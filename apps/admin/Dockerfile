FROM node:latest

WORKDIR /workspace

RUN npm install --global pnpm@11.19.0

COPY package.json pnpm-workspace.yaml pnpm-lock.yaml ./
COPY apps/admin/package.json apps/admin/package.json

RUN pnpm install --frozen-lockfile

COPY apps/admin apps/admin

RUN pnpm --filter admin build

ENV NODE_ENV=production
ENV HOST=0.0.0.0
ENV PORT=3001

EXPOSE 3001

CMD ["node", "apps/admin/.output/server/index.mjs"]
