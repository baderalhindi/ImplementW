# The e2e stack's stand-ins (TASK-085; e2e/stack/standins/standins.mjs). Build context: repository root.
# Node for the HTTP and SMTP stand-ins, psql for the scanner pass and the named fixtures. Docker Hub's node through
# Google's public mirror (PR #78).
FROM mirror.gcr.io/library/node:24-slim
RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /standins
COPY src/frontend/e2e/stack/standins/ ./
USER node
EXPOSE 2525 5397 8025
CMD ["node", "standins.mjs"]
