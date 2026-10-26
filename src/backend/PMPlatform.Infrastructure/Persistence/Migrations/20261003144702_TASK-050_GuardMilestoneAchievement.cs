using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-050, 6 of 6: the achievement history held in the database, whoever writes. A revision is born DRAFT, after every
    /// earlier revision of its milestone, and moves only along the edges of <c>MilestoneAchievementWorkflow</c>. Its claim changes
    /// only while DRAFT. An ACCEPTED revision is never edited: it changes only by being superseded by a later revision of the same
    /// milestone, keeping its accepted date and everything else. RETURNED and SUPERSEDED are final, and only a DRAFT is deleted.
    /// With the partial unique indexes of 3 of 6, a milestone has at most one ACCEPTED and one open revision, for any interleaving
    /// of writers. The table is never truncated. Reads only the <c>milestone</c> schema (README R-7).
    /// </summary>
    public partial class TASK050_GuardMilestoneAchievement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION milestone.guard_milestone_achievement() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'milestone_achievement % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'milestone_achievement %: a revision is born DRAFT', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_milestone_achievement_born';
                        END IF;

                        IF EXISTS (SELECT 1 FROM milestone.milestone_achievement a
                                   WHERE a.project_milestone_id = NEW.project_milestone_id AND a.revision_no >= NEW.revision_no) THEN
                            RAISE EXCEPTION 'milestone_achievement %: a new revision comes after every revision of its milestone', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_milestone_achievement_revision_order';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_milestone_id, NEW.project_id, NEW.revision_no, NEW.project_intake_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_milestone_id, OLD.project_id, OLD.revision_no, OLD.project_intake_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'milestone_achievement %: its milestone, project and revision never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('RETURNED', 'SUPERSEDED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'milestone_achievement % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'ACCEPTED'
                       AND to_jsonb(NEW) - ARRAY['status', 'superseded_by_achievement_id', 'updated_at', 'updated_by']
                           IS DISTINCT FROM to_jsonb(OLD) - ARRAY['status', 'superseded_by_achievement_id', 'updated_at', 'updated_by'] THEN
                        RAISE EXCEPTION 'milestone_achievement % is ACCEPTED: it is never edited, a correction is a new revision', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status <> 'DRAFT'
                       AND (NEW.claimed_achievement_date, NEW.narrative, NEW.narrative_lang, NEW.submitted_by_user_id, NEW.submitted_at)
                           IS DISTINCT FROM (OLD.claimed_achievement_date, OLD.narrative, OLD.narrative_lang, OLD.submitted_by_user_id, OLD.submitted_at) THEN
                        RAISE EXCEPTION 'milestone_achievement % is %: the claim WF-11 reviewed never changes', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status THEN
                        IF (OLD.status, NEW.status) NOT IN
                           (('DRAFT', 'SUBMITTED'), ('SUBMITTED', 'ACCEPTED'), ('SUBMITTED', 'RETURNED'), ('ACCEPTED', 'SUPERSEDED')) THEN
                            RAISE EXCEPTION 'milestone_achievement %: % to % is not a step of its workflow', OLD.id, OLD.status, NEW.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF NEW.status = 'ACCEPTED' AND NEW.accepted_actual_achievement_date IS DISTINCT FROM NEW.claimed_achievement_date THEN
                            RAISE EXCEPTION 'milestone_achievement %: the accepted date is the date the revision claimed', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_milestone_achievement_accepted_date';
                        END IF;

                        IF NEW.status = 'SUPERSEDED'
                           AND NOT EXISTS (SELECT 1 FROM milestone.milestone_achievement a
                                           WHERE a.id = NEW.superseded_by_achievement_id
                                             AND a.project_milestone_id = NEW.project_milestone_id AND a.revision_no > NEW.revision_no) THEN
                            RAISE EXCEPTION 'milestone_achievement %: it is superseded by a later revision of its milestone', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_milestone_achievement_superseded_by';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_milestone_achievement
                    BEFORE INSERT OR UPDATE OR DELETE ON milestone.milestone_achievement
                    FOR EACH ROW EXECUTE FUNCTION milestone.guard_milestone_achievement();

                CREATE FUNCTION milestone.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'milestone.% holds achievement history and is never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON milestone.milestone_achievement
                    FOR EACH STATEMENT EXECUTE FUNCTION milestone.refuse_truncation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER refuse_truncation ON milestone.milestone_achievement;
                DROP FUNCTION milestone.refuse_truncation();
                DROP TRIGGER guard_milestone_achievement ON milestone.milestone_achievement;
                DROP FUNCTION milestone.guard_milestone_achievement();
                """);
        }
    }
}
