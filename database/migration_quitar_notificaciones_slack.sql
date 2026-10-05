-- El sistema no envía notificaciones a Slack ni a Microsoft Teams; la única alerta automática es el
-- correo electrónico ante una recomendación "No desplegar". Se elimina el indicador sin uso del plan.
ALTER TABLE planes_suscripcion DROP COLUMN IF EXISTS notificaciones_slack;
