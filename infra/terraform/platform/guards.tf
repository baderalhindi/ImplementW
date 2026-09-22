# Preflight (TASK-017).
#
# ADR-001 §9 holds every apply — DEV included — until the written confirmation (R-1), the region
# (R-2) and the tenancy (R-3) close, and the region cannot be corrected after provisioning without
# rebuilding every environment. These preconditions fail the *plan*, before anything is created,
# and each names the residual item that owes the value. They are the Terraform counterpart of the
# guards in infra/environments/lib/common.sh, which hold the same line for the gcloud scripts.
#
# terraform_data uses no provider and needs no credentials, so the refusal is the first thing a
# plan reports rather than an authentication error some way into it.

resource "terraform_data" "preflight" {
  input = {
    environment = var.environment
    project_id  = local.environment.project_id
    region      = local.platform.region
  }

  lifecycle {
    precondition {
      condition     = local.platform.region != ""
      error_message = "platform.region is empty in ${var.manifest_path}. The in-Kingdom region name is UGV-07, owed by AHDA IT (ADR-001 R-2). Nothing is provisioned until it is named."
    }

    precondition {
      condition     = contains(local.platform.region_allowlist, local.platform.region)
      error_message = "region '${local.platform.region}' is not on platform.region_allowlist. ADR-001 C-1: no resource in any region outside Saudi Arabia. me-central1 is Doha, Qatar."
    }

    precondition {
      condition     = local.platform.organization_id != ""
      error_message = "platform.organization_id is empty in ${var.manifest_path} — UGV-07, owed by AHDA IT (ADR-001 R-3, tenancy ownership). CTL-47: the platform sits in AHDA's own GCP organisation, and if the answer is the vendor's, ADR-001 §6 says the decision is re-taken and this configuration is re-written, not amended."
    }

    precondition {
      condition     = local.domain_name != ""
      error_message = "APP_BASE_URL for ${var.environment} is empty in ${var.manifest_path}. No AHDA-owned DNS zone is named anywhere in the workbook (environment-separation.md F-1, proposed UGV-12), and the load balancer's certificate cannot be issued for a hostname that does not exist."
    }

    # OQ-003 / PTBC-048 is open, so no retention period is invented. DEV, SIT and UAT may run on
    # the provider default while it is; PROD may not, because "how long a production backup is
    # kept" is not a value to discover after the fact.
    precondition {
      condition     = var.environment != "prod" || var.retained_backups != null
      error_message = "retained_backups is null for PROD. The backup retention period is OQ-003 / PTBC-048, owed by AHDA Cybersecurity and Records before production. Set it from the approved value; do not invent one."
    }
  }
}
