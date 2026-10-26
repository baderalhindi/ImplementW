# Frontend dev-server image (TASK-014). Build context: repository root.
#
# Local development only: it runs Vite's dev server with the repository's src/frontend bind-mounted over /repo/src/frontend
# (docker-compose.yml), so edits on the host hot-reload in the browser. node_modules is installed into the image at
# build time and kept in a volume so the host's node_modules (if any) never leaks in. The deployable frontend is static
# assets from `npm run build` (ADR-002 §4.1) and is not this image; TASK-017/TASK-018 build that artifact.
# Docker Hub's node through Google's public mirror (same digest): CI builds this image for the e2e stack (TASK-085), and
# GitHub's shared runners exhaust Docker Hub's anonymous pull limit (PR #78).
FROM mirror.gcr.io/library/node:24-slim
ENV NODE_ENV=development
WORKDIR /repo/src/frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY src/frontend/ ./
EXPOSE 5173
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
