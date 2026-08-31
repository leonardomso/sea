FROM node:latest

WORKDIR /workspace

RUN npm install --global pnpm@11.19.0

COPY package.json pnpm-workspace.yaml pnpm-lock.yaml ./
COPY apps/admin/package.json apps/admin/package.json

RUN pnpm install --frozen-lockfile

COPY apps/admin apps/admin

EXPOSE 3001

CMD ["pnpm", "--filter", "admin", "dev"]
