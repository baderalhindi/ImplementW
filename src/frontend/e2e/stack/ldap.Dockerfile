# The e2e stack's directory (TASK-085). Build context: repository root.
#
# The test directory of infra/docker/ldap with the e2e suite's people added (e2e/stack/ldap/e2e-people.ldif). Same base image,
# same slapd configuration, same load at build time, so it differs from AHDA-ldap only by those people.
FROM mirror.gcr.io/library/alpine:3.22
RUN apk add --no-cache openldap openldap-back-mdb openldap-clients
COPY infra/docker/ldap/slapd.conf /etc/openldap/slapd.conf
COPY infra/docker/ldap/directory.ldif /tmp/directory.ldif
COPY src/frontend/e2e/stack/ldap/e2e-people.ldif /tmp/e2e-people.ldif
RUN mkdir -p /run/openldap /var/lib/openldap/openldap-data \
    && slapadd -f /etc/openldap/slapd.conf -l /tmp/directory.ldif \
    && slapadd -f /etc/openldap/slapd.conf -l /tmp/e2e-people.ldif \
    && rm /tmp/directory.ldif /tmp/e2e-people.ldif \
    && chown -R ldap:ldap /run/openldap /var/lib/openldap
EXPOSE 389
CMD ["slapd", "-f", "/etc/openldap/slapd.conf", "-h", "ldap:///", "-u", "ldap", "-g", "ldap", "-d", "256"]
