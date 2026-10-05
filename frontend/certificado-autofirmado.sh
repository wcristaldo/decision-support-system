#!/bin/sh
# Si no se montó un certificado en /etc/nginx/certs (tls.crt y tls.key), genera uno autofirmado
# para que el contenedor pueda servir HTTPS. En producción se reemplaza por el certificado institucional.
set -e
DIR=/etc/nginx/certs
if [ ! -f "$DIR/tls.crt" ] || [ ! -f "$DIR/tls.key" ]; then
    mkdir -p "$DIR"
    openssl req -x509 -nodes -newkey rsa:2048 -days 365 \
        -subj "/CN=${TLS_CN:-localhost}" \
        -keyout "$DIR/tls.key" -out "$DIR/tls.crt" 2>/dev/null
    echo "certificado-autofirmado: generado para ${TLS_CN:-localhost} (reemplazar por el certificado institucional)"
fi
