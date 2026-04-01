FROM nginx:alpine

COPY Lab1_Frontend/index.html /usr/share/nginx/html/index.html
COPY Lab1_Frontend/styles.css /usr/share/nginx/html/styles.css
COPY Lab1_Frontend/app.js /usr/share/nginx/html/app.js
COPY Lab1_Frontend/app-config.js /usr/share/nginx/html/app-config.js
COPY Lab1_Frontend/app-config.template.js /usr/share/nginx/html/app-config.template.js
COPY Lab1_Frontend/default.conf.template /etc/nginx/templates/default.conf.template
COPY Lab1_Frontend/40-env-config.sh /docker-entrypoint.d/40-env-config.sh

RUN chmod +x /docker-entrypoint.d/40-env-config.sh