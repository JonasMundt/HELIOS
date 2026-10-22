
# ------------------------------------------
# HELIOS FRONTEND - BUILD
# ------------------------------------------

FROM node:24-alpine AS build

WORKDIR /app

# Install dependencies
COPY package.json package-lock.json ./
RUN npm ci

# Build React application
COPY . .
RUN npm run build

# ------------------------------------------
# HELIOS FRONTEND - NGINX
# ------------------------------------------

FROM nginx:stable-alpine AS runtime

# Nginx reverse proxy configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy compiled React application
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
