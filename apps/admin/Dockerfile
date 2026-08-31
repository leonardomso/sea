FROM node:latest

WORKDIR /workspace

RUN corepack enable && corepack prepare pnpm@11.19.0 --activate

COPY package.json pnpm-workspace.yaml pnpm-lock.yaml ./
COPY apps/admin/package.json apps/admin/package.json

RUN pnpm install --frozen-lockfile

COPY apps/admin apps/admin

EXPOSE 3001

CMD ["pnpm", "--filter", "admin", "dev"]
