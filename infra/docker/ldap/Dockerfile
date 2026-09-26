# AHDA-ldap: the local and CI test directory (TASK-028). Build context: infra/docker/ldap.
#
# An OpenLDAP server holding one synthetic person per role R01–R08 (the same people as the local user seed,
# infra/docker/postgres/seed/seed-local-users.sql), linked to their platform users by entryUUID, and the service account
# the API searches with. It stands in for AHDA's Active Directory, whose product and topology AHDA IT confirms at
# environment setup (ADR-007); the API reaches both through the same LDAP adapter, configured by attribute names.
#
# Nothing here is valid outside a developer's machine or a CI runner: plain LDAP on a private port, and passwords that are
# labelled local defaults. The data is loaded at build time, so every container starts from the same directory.
FROM alpine:3.22
RUN apk add --no-cache openldap openldap-back-mdb openldap-clients
COPY slapd.conf /etc/openldap/slapd.conf
COPY directory.ldif /tmp/directory.ldif
RUN mkdir -p /run/openldap /var/lib/openldap/openldap-data \
    && slapadd -f /etc/openldap/slapd.conf -l /tmp/directory.ldif \
    && rm /tmp/directory.ldif \
    && chown -R ldap:ldap /run/openldap /var/lib/openldap
EXPOSE 389
# Binds 389 as root, then runs as the ldap user. -d 256 keeps it in the foreground and logs each operation (never a password).
CMD ["slapd", "-f", "/etc/openldap/slapd.conf", "-h", "ldap:///", "-u", "ldap", "-g", "ldap", "-d", "256"]
