#!/bin/sh
set -eu

envsubst '${BACKEND_URL}' < /usr/share/nginx/html/app-config.template.js > /usr/share/nginx/html/app-config.js
