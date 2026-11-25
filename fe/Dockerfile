# syntax=docker/dockerfile:1
# Frontend NX-MTD (Vite + React) phục vụ bằng nginx. Build context: thư mục fe/

# Dùng bản Debian (glibc) giống CI (ubuntu) vì Vite 8/rolldown có binary native theo từng nền tảng
FROM node:24-bookworm-slim AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Biến VITE_* được "nướng" vào bundle lúc build.
# Để rỗng = gọi API cùng domain qua đường dẫn /api (nginx sẽ chuyển tiếp sang backend).
ARG VITE_API_BASE_URL=""
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}

RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
