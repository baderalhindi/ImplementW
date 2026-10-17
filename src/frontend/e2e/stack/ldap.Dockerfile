# The e2e stack's directory (TASK-085). Build context: repository root.
#
# The test directory of infra/docker/ldap with the e2e suite's people added (e2e/stack/ldap/e2e-people.ldif). Same base image,
# same slapd configuration, same load at build time, so it differs from AHDA-ldap only by those people.
#
# LOAD_PEOPLE=1 also adds the load test's 140 people (TASK-086, docs/testing/load-test.md §4.1): load.u001…load.u140, whose
# entryUUID is the directory subject docs/testing/load-test/volume/10-organisation.sql stores. Their departments and groups
# follow that file: 001–010 Portfolio, 011–036 two per department, 037–111 Project Managers round-robin over the 13
# departments, 112–131 entity Project Managers (ou=External), 132–140 Executive and Viewer. The password is a LOCAL DEFAULT.
FROM mirror.gcr.io/library/alpine:3.22
ARG LOAD_PEOPLE=0
RUN apk add --no-cache openldap openldap-back-mdb openldap-clients
COPY infra/docker/ldap/slapd.conf /etc/openldap/slapd.conf
COPY infra/docker/ldap/directory.ldif /tmp/directory.ldif
COPY src/frontend/e2e/stack/ldap/e2e-people.ldif /tmp/e2e-people.ldif
RUN mkdir -p /run/openldap /var/lib/openldap/openldap-data \
    && slapadd -f /etc/openldap/slapd.conf -l /tmp/directory.ldif \
    && slapadd -f /etc/openldap/slapd.conf -l /tmp/e2e-people.ldif \
    && rm /tmp/directory.ldif /tmp/e2e-people.ldif \
    && if [ "$LOAD_PEOPLE" = "1" ]; then \
         for n in $(seq 1 140); do \
           u=$(printf 'load.u%03d' "$n"); \
           if [ "$n" -le 10 ] || [ "$n" -ge 132 ]; then d=1; \
           elif [ "$n" -le 36 ]; then d=$(( (n - 11) / 2 + 1 )); \
           elif [ "$n" -le 111 ]; then d=$(( (n - 37) % 13 + 1 )); \
           else d=0; fi; \
           if [ "$d" -eq 0 ]; then printf 'dn: uid=%s,ou=External,dc=pmplatform,dc=local\n' "$u"; \
           else printf 'dn: uid=%s,ou=People,dc=pmplatform,dc=local\ndepartmentNumber: LOAD-D%02d\n' "$u" "$d"; fi; \
           printf 'objectClass: inetOrgPerson\nuid: %s\ncn: Load User %03d\nsn: User%03d\ndisplayName: Load User %03d\nmail: %s@pmplatform.local\nentryUUID: 00000000-10a3-4000-8000-%012d\nuserPassword: load-directory-password\n\n' \
             "$u" "$n" "$n" "$n" "$u" "$n"; \
         done > /tmp/load-people.ldif \
         && slapadd -f /etc/openldap/slapd.conf -l /tmp/load-people.ldif && rm /tmp/load-people.ldif; \
       fi \
    && chown -R ldap:ldap /run/openldap /var/lib/openldap
EXPOSE 389
CMD ["slapd", "-f", "/etc/openldap/slapd.conf", "-h", "ldap:///", "-u", "ldap", "-g", "ldap", "-d", "256"]
