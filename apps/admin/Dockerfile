FROM node:24.19.0-bookworm-slim@sha256:a9f5f7c91a432850b2a8a7797adf5eadb6c733ceed61167806cee7ea7fbc29df

WORKDIR /workspace

RUN npm install --global pnpm@11.25.0

COPY package.json pnpm-workspace.yaml pnpm-lock.yaml ./
COPY apps/admin/package.json apps/admin/package.json

RUN pnpm install --frozen-lockfile

COPY apps/admin apps/admin

RUN pnpm --filter admin run build

ENV NODE_ENV=production
ENV HOST=0.0.0.0
ENV PORT=3001

EXPOSE 3001

CMD ["node", "apps/admin/.output/server/index.mjs"]
