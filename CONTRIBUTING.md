# Cómo contribuir

## Flujo de trabajo

```
main        ← producción, merge solo via PR desde develop
develop     ← integración
feature/*   ← nueva funcionalidad  →  PR a develop
fix/*       ← bug fix              →  PR a develop
```

```bash
git checkout develop && git pull
git checkout -b feature/nombre-corto
# ... trabajar ...
git commit -m "feat: descripción"
git push origin feature/nombre-corto
# Abrir PR hacia develop
```

## Convención de commits

| Prefijo | Cuándo usarlo |
|---------|---------------|
| `feat:` | Nueva funcionalidad |
| `fix:` | Corrección de bug |
| `docs:` | Solo documentación |
| `refactor:` | Refactorización sin cambio de comportamiento |
| `chore:` | Mantenimiento (deps, build, config) |

## Variables de entorno

Nunca commitear `.env` ni `appsettings.Development.json`.  
Usar siempre `.env.example` como plantilla.
