using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DecisionSupportAPI.Services;

/// <summary>
/// Diseño del reporte ejecutivo en PDF. Todo texto está pensado para alguien que no lee el
/// reporte técnico de Robot Framework: frases cortas, sin jerga y con el "qué hacer ahora".
/// </summary>
public static class ReporteEjecutivoPdf
{
    private const string Nav = "#1c2b3a", Azul = "#2980b9", Borde = "#d5d8dc", Suave = "#5d6d7e", Blanco = "#ffffff";
    private const string Verde = "#1a7a4e", Rojo = "#c0392b", Ambar = "#b7770d";
    private const string FondoVerde = "#eaf6ef", FondoRojo = "#fdecea", FondoAmbar = "#fff8e6", FondoGris = "#f4f6f8", FondoAzul = "#eaf2fb";

    private static readonly CultureInfo EsPy = new("es-PY");

    // ── Formatos y textos (públicos para poder probarlos) ──────────────────────────
    public static string Pct(decimal? v) => v.HasValue ? v.Value.ToString("0.0", EsPy) + " %" : "-";
    public static string Segundos(decimal? v) => v.HasValue ? v.Value.ToString("0.##", EsPy) + " s" : "-";
    public static string Fecha(DateTime f) => f.ToLocalTime().ToString("dd/MM/yyyy HH:mm", EsPy);

    public static string NombrePerfil(PerfilReporte p) => p switch
    {
        PerfilReporte.Gerencia => "Resumen para Gerencia",
        PerfilReporte.Tecnico => "Informe para la decisión (Líder Técnico)",
        _ => "Informe de calidad (Analista QA)",
    };

    public static string NombreRecomendacion(string rec) => rec switch
    {
        "DESPLEGAR" => "DESPLEGAR",
        "REVISAR" => "REVISAR",
        "NO_DESPLEGAR" => "NO DESPLEGAR",
        _ => "SIN EVALUACIÓN",
    };

    public static string FraseResultado(string rec) => rec switch
    {
        "DESPLEGAR" => "La versión cumple los umbrales de calidad definidos para el proyecto. No se detectaron objeciones para desplegarla.",
        "REVISAR" => "La versión no alcanza algún umbral, pero está dentro del margen de tolerancia (±5 %). Conviene revisarla antes de desplegarla.",
        "NO_DESPLEGAR" => "La versión no alcanza el mínimo de calidad en uno o más indicadores. No se recomienda desplegarla hasta corregirlos.",
        _ => "Esta versión todavía no tiene una evaluación de calidad.",
    };

    public static (string Titulo, string Detalle) RiesgoNegocio(string rec) => rec switch
    {
        "DESPLEGAR" => ("Riesgo bajo", "El sistema no detectó objeciones de calidad para esta versión."),
        "REVISAR" => ("Riesgo moderado", "Hay indicadores dentro del margen de tolerancia que conviene monitorear antes de avanzar."),
        "NO_DESPLEGAR" => ("Riesgo alto", "Uno o más indicadores están por debajo del mínimo aceptable de calidad."),
        _ => ("Sin evaluar", "Todavía no hay resultados evaluados para estimar el riesgo."),
    };

    private static string ColorRec(string rec) => rec switch { "DESPLEGAR" => Verde, "REVISAR" => Ambar, "NO_DESPLEGAR" => Rojo, _ => Suave };
    private static string FondoRec(string rec) => rec switch { "DESPLEGAR" => FondoVerde, "REVISAR" => FondoAmbar, "NO_DESPLEGAR" => FondoRojo, _ => FondoGris };
    private static string TextoRecCorto(string rec) => rec switch { "DESPLEGAR" => "Desplegar", "REVISAR" => "Revisar", "NO_DESPLEGAR" => "No desplegar", _ => "Sin evaluar" };

    private static string NombreVeredicto(string v) => v switch { "cumple" => "Cumple", "revisar" => "Revisar", "no_cumple" => "No cumple", _ => "No aplica" };
    private static string ColorVeredicto(string v) => v switch { "cumple" => Verde, "revisar" => Ambar, "no_cumple" => Rojo, _ => Suave };

    private static bool EsMayorIgual(string criterio) => criterio is "tasa_exito" or "cobertura";

    private static string NombreCriterio(ReporteCriterio c) => c.Criterio switch
    {
        "tasa_exito" => "Tasa de éxito",
        "cobertura" => "Cobertura de ejecución",
        "tasa_fallo" => "Tasa de fallo",
        "tiempo_ejecucion" => "Tiempo de ejecución",
        _ => string.IsNullOrWhiteSpace(c.Nombre) ? c.Criterio : c.Nombre,
    };

    private static string FormatoCriterio(ReporteCriterio c, decimal? v) =>
        v == null ? "-" : c.Criterio == "tiempo_ejecucion" ? Segundos(v) : Pct(v);

    private static string Exigencia(ReporteCriterio c)
    {
        if (c.Umbral == null) return "-";
        var valor = c.Criterio == "tiempo_ejecucion" ? Segundos(c.Umbral) : Pct(c.Umbral);
        return (EsMayorIgual(c.Criterio) ? "mínimo " : "máximo ") + valor;
    }

    /// <summary>Qué hacer ahora, en frases directas, a partir de los criterios que no se cumplen.</summary>
    public static List<string> Acciones(ReporteEjecutivoData d)
    {
        var acciones = new List<string>();
        var fallan = d.Fallidas is int nf ? $"las {nf} pruebas que fallan" : "las pruebas que fallan";
        var omitidas = d.Omitidas is int no ? $"las {no} pruebas omitidas" : "las pruebas omitidas";
        foreach (var c in d.Criterios.Where(x => x.Veredicto is "no_cumple" or "revisar"))
        {
            var critico = c.Veredicto == "no_cumple";
            switch (c.Criterio)
            {
                case "tasa_exito":
                    acciones.Add(critico
                        ? $"Corregir {fallan} y volver a ejecutar el plan: la tasa de éxito ({Pct(c.Valor)}) está por debajo del {Exigencia(c)}."
                        : $"La tasa de éxito ({Pct(c.Valor)}) está apenas por debajo del {Exigencia(c)}: revisar {fallan} antes de desplegar.");
                    break;
                case "cobertura":
                    acciones.Add(critico
                        ? $"Ejecutar {omitidas} (o justificar por qué se omiten): solo se ejecutó el {Pct(c.Valor)} del plan y se exige {Exigencia(c)}."
                        : $"Casi todo el plan se ejecutó ({Pct(c.Valor)} frente a un {Exigencia(c)}): completar {omitidas}.");
                    break;
                case "tasa_fallo":
                    acciones.Add(critico
                        ? $"Reducir los fallos: el {Pct(c.Valor)} de las pruebas falla y se tolera un {Exigencia(c)}."
                        : $"La tasa de fallo ({Pct(c.Valor)}) está cerca del límite ({Exigencia(c)}): revisar {fallan}.");
                    break;
                case "tiempo_ejecucion":
                    acciones.Add(critico
                        ? $"La ejecución tardó {Segundos(c.Valor)} y se exige un {Exigencia(c)}: revisar las pruebas lentas o la infraestructura de pruebas."
                        : $"El tiempo de ejecución ({Segundos(c.Valor)}) está cerca del límite ({Exigencia(c)}): conviene revisar las pruebas más lentas.");
                    break;
                default:
                    acciones.Add($"Revisar el criterio «{NombreCriterio(c)}»: {c.Observacion}");
                    break;
            }
        }

        if (d.Detalle is { TotalFallidas: > 0 } det && det.Modulos.FirstOrDefault(m => m.Fallidas > 0) is { } peor)
            acciones.Add($"Empezar por el módulo «{peor.Nombre}»: concentra {peor.Fallidas} de las {det.TotalFallidas} pruebas que fallan.");

        if (d.Recomendacion == "SIN_EVALUACION")
            acciones.Add("Cargar los resultados de las pruebas para obtener una evaluación.");
        else if (acciones.Count == 0)
            acciones.Add("No hay acciones de calidad pendientes: todos los criterios evaluados se cumplen.");

        if (d.Recomendacion != "SIN_EVALUACION" && d.Decision == null)
            acciones.Add("Registrar la decisión de despliegue (Aprobado o Rechazado) con su justificación.");
        return acciones;
    }

    private static string VerboDecision(string decision) => decision.ToLowerInvariant() switch
    {
        "aprobado" => "aprobó",
        "rechazado" => "rechazó",
        "postergado" => "postergó la decisión sobre",
        _ => "registró una decisión sobre",
    };

    private static string FraseConteo(ReporteEjecutivoData d) =>
        d.Total is int t
            ? $"De las {t} pruebas del plan, {d.Exitosas ?? 0} pasaron, {d.Fallidas ?? 0} fallaron y {d.Omitidas ?? 0} no se ejecutaron."
            : "No hay conteo de pruebas para esta evaluación.";

    // ── Documento ─────────────────────────────────────────────────────────────────
    public static byte[] Generar(ReporteEjecutivoData d, PerfilReporte perfil, string solicitante)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var generado = DateTime.UtcNow;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(1.8f, Unit.Centimetre);
                page.MarginTop(1.4f, Unit.Centimetre);
                page.MarginBottom(1.4f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Arial").FontColor(Nav));

                page.Content().Column(col =>
                {
                    col.Spacing(9);
                    Encabezado(col, d, perfil, generado);
                    Semaforo(col, d);
                    Indicadores(col, d);

                    switch (perfil)
                    {
                        case PerfilReporte.Gerencia: SeccionesGerencia(col, d); break;
                        case PerfilReporte.Tecnico: SeccionesTecnico(col, d); break;
                        default: SeccionesQa(col, d); break;
                    }

                    col.Item().Background(FondoAzul).Border(1f).BorderColor(Azul).Padding(9).Text(
                        "Este reporte resume, en lenguaje claro, el resultado de las pruebas automatizadas evaluadas por Roshka DSS. " +
                        "El sistema asiste con una recomendación basada en métricas de calidad y umbrales configurables por proyecto; " +
                        "la decisión final de despliegue es siempre de una persona.").FontSize(8.5f).Italic();
                });

                page.Footer().BorderTop(0.5f).BorderColor(Borde).PaddingTop(5).Row(r =>
                {
                    r.RelativeItem().Text($"Roshka DSS  ·  Roshka S.A.  ·  Generado el {Fecha(generado)} por {solicitante}")
                        .FontSize(7.5f).FontColor("#8a949e");
                    r.AutoItem().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor("#8a949e"));
                        t.Span("Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }

    // ── Bloques comunes ───────────────────────────────────────────────────────────
    private static void Encabezado(ColumnDescriptor col, ReporteEjecutivoData d, PerfilReporte perfil, DateTime generado)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(54).Height(48).Border(1f).BorderColor(Borde).AlignCenter().AlignMiddle()
               .Text("DSS").FontSize(13).Bold();
            row.ConstantItem(12);
            row.RelativeItem().AlignMiddle().Column(c =>
            {
                c.Item().Text("REPORTE EJECUTIVO DE CALIDAD").FontSize(15).Bold();
                c.Item().PaddingTop(2).Text(NombrePerfil(perfil)).FontSize(10).FontColor(Azul).Bold();
                c.Item().Text("Roshka DSS - Sistema de Apoyo a la Toma de Decisiones").FontSize(8f).FontColor(Suave).Italic();
            });
            row.ConstantItem(12);
            row.ConstantItem(140).Border(1.5f).BorderColor(Azul).Padding(8).Column(c =>
            {
                c.Item().Text("N° de reporte").FontSize(7.5f).FontColor(Suave);
                c.Item().Text($"RPT-{d.ResultadoId:D6}").FontSize(9).Bold().FontColor(Azul);
                c.Item().PaddingTop(4).Text($"Emitido: {Fecha(generado)}").FontSize(7.5f);
            });
        });

        col.Item().LineHorizontal(1f).LineColor(Borde);

        col.Item().Row(row =>
        {
            void Dato(string etiqueta, string valor) => row.RelativeItem().Column(c =>
            {
                c.Item().Text(etiqueta).FontSize(7.5f).FontColor(Suave);
                c.Item().Text(valor).Bold();
            });
            Dato("Proyecto", d.Proyecto);
            Dato("Versión", "v" + d.Version);
            Dato("Evaluación", Fecha(d.FechaEvaluacion ?? d.FechaCarga));
        });
    }

    private static void Semaforo(ColumnDescriptor col, ReporteEjecutivoData d)
    {
        var color = ColorRec(d.Recomendacion);
        col.Item().Background(FondoRec(d.Recomendacion)).Border(1.5f).BorderColor(color).Padding(12).Row(row =>
        {
            row.ConstantItem(190).AlignMiddle().Column(c =>
            {
                c.Item().Text("Recomendación del sistema").FontSize(8).FontColor(Suave);
                c.Item().Text(NombreRecomendacion(d.Recomendacion)).FontSize(19).Bold().FontColor(color);
            });
            row.ConstantItem(10);
            row.RelativeItem().AlignMiddle().Text(FraseResultado(d.Recomendacion)).FontSize(10.5f);
        });
    }

    private static void Indicadores(ColumnDescriptor col, ReporteEjecutivoData d)
    {
        ReporteCriterio? Criterio(string clave) => d.Criterios.FirstOrDefault(c => c.Criterio == clave);

        void Tarjeta(RowDescriptor row, string titulo, string valor, string clave)
        {
            var c = Criterio(clave);
            row.RelativeItem().Border(1f).BorderColor(Borde).Padding(8).Column(k =>
            {
                k.Item().Text(titulo).FontSize(8).FontColor(Suave);
                k.Item().PaddingTop(2).Text(valor).FontSize(16).Bold();
                if (c != null)
                {
                    k.Item().PaddingTop(2).Text(Exigencia(c)).FontSize(7.5f).FontColor(Suave);
                    k.Item().PaddingTop(2).Text(NombreVeredicto(c.Veredicto)).FontSize(8.5f).Bold().FontColor(ColorVeredicto(c.Veredicto));
                }
            });
        }

        col.Item().Row(row =>
        {
            row.Spacing(7);
            Tarjeta(row, "Tasa de éxito", Pct(d.TasaExito), "tasa_exito");
            Tarjeta(row, "Cobertura de ejecución", Pct(d.Cobertura), "cobertura");
            Tarjeta(row, "Tasa de fallo", Pct(d.TasaFallo), "tasa_fallo");
            Tarjeta(row, "Tiempo de ejecución", Segundos(d.Tiempo), "tiempo_ejecucion");
        });
        col.Item().Text(FraseConteo(d)).FontSize(9).FontColor(Suave);
    }

    private static void Seccion(ColumnDescriptor col, string titulo, Action<ColumnDescriptor> contenido)
    {
        col.Item().EnsureSpace(80).Column(c =>
        {
            c.Item().Text(titulo.ToUpperInvariant()).FontSize(9).Bold().FontColor(Azul);
            c.Item().PaddingTop(2).LineHorizontal(1f).LineColor(Borde);
            c.Item().PaddingTop(6).Column(inner => { inner.Spacing(4); contenido(inner); });
        });
    }

    private static IContainer Celda(IContainer c) => c.BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(4).PaddingHorizontal(6);
    private static IContainer Cabecera(IContainer c) => c.Background(FondoAzul).PaddingVertical(4).PaddingHorizontal(6);

    private static void Vinetas(ColumnDescriptor col, IEnumerable<string> textos)
    {
        foreach (var t in textos)
            col.Item().Row(r =>
            {
                r.ConstantItem(12).Text("•").Bold().FontColor(Azul);
                r.RelativeItem().Text(t);
            });
    }

    private static void EstadoDecision(ColumnDescriptor col, ReporteEjecutivoData d)
    {
        if (d.Decision is { } dec)
        {
            col.Item().Text(t =>
            {
                t.Span($"El {Fecha(dec.Fecha)} {dec.Usuario} {VerboDecision(dec.Decision)} este despliegue");
                t.Span(dec.EsOverride
                    ? " en contra de la recomendación del sistema, bajo su responsabilidad explícita."
                    : ", en línea con la recomendación del sistema.");
            });
            if (!string.IsNullOrWhiteSpace(dec.Comentario))
                col.Item().Text(t => { t.Span("Justificación: ").Bold(); t.Span(dec.Comentario); });
        }
        else
        {
            col.Item().Text(d.Recomendacion == "SIN_EVALUACION"
                ? "No corresponde: la versión todavía no fue evaluada."
                : "Pendiente: todavía no se registró la decisión de despliegue (Aprobado o Rechazado).");
        }
    }

    private static void TablaHistorial(ColumnDescriptor col, ReporteEjecutivoData d, int maximo)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.1f); c.RelativeColumn(1.8f); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1.4f);
            });
            table.Header(h =>
            {
                foreach (var t in new[] { "Versión", "Fecha de carga", "Éxito", "Cobertura", "Recomendación" })
                    h.Cell().Element(Cabecera).Text(t).Bold().FontSize(8.5f);
            });
            foreach (var h in d.Historial.Take(maximo))
            {
                table.Cell().Element(Celda).Text("v" + h.Version);
                table.Cell().Element(Celda).Text(Fecha(h.Fecha));
                table.Cell().Element(Celda).Text(Pct(h.TasaExito));
                table.Cell().Element(Celda).Text(Pct(h.Cobertura));
                table.Cell().Element(Celda).Text(TextoRecCorto(h.Recomendacion)).Bold().FontColor(ColorRec(h.Recomendacion));
            }
        });
    }

    private static void TablaCriterios(ColumnDescriptor col, ReporteEjecutivoData d)
    {
        if (d.Criterios.Count == 0)
        {
            col.Item().Text("No hay criterios evaluados para esta versión.");
            return;
        }
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2.2f); c.RelativeColumn(1.2f); c.RelativeColumn(1.4f); c.RelativeColumn(1.1f); });
            table.Header(h =>
            {
                foreach (var t in new[] { "Criterio", "Obtenido", "Exigido", "Estado" })
                    h.Cell().Element(Cabecera).Text(t).Bold().FontSize(8.5f);
            });
            foreach (var c in d.Criterios)
            {
                table.Cell().Element(Celda).Text(NombreCriterio(c));
                table.Cell().Element(Celda).Text(FormatoCriterio(c, c.Valor)).Bold();
                table.Cell().Element(Celda).Text(Exigencia(c));
                table.Cell().Element(Celda).Text(NombreVeredicto(c.Veredicto)).Bold().FontColor(ColorVeredicto(c.Veredicto));
            }
        });
    }

    private static bool HayFallas(ReporteEjecutivoData d) => (d.Fallidas ?? 0) > 0 || (d.Detalle?.TotalFallidas ?? 0) > 0;

    private static string ResumenModulosConFallas(DetalleRobotFramework det) =>
        string.Join(", ", det.Modulos.Where(m => m.Fallidas > 0).Take(3).Select(m => $"{m.Nombre} ({m.Fallidas})"));

    private static void TablaFallidas(ColumnDescriptor col, ReporteEjecutivoData d, int maximo)
    {
        var det = d.Detalle;
        if (det == null)
        {
            col.Item().Text("Esta carga no incluye el detalle por prueba. Se obtiene al cargar el output.json completo de Robot Framework.")
                .FontSize(8.5f).FontColor(Suave).Italic();
            return;
        }
        if (det.TotalFallidas == 0)
        {
            col.Item().Text("Ninguna prueba falló en esta ejecución.");
            return;
        }

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(1.4f); c.RelativeColumn(2.6f); c.RelativeColumn(3f); });
            table.Header(h =>
            {
                foreach (var t in new[] { "Módulo", "Prueba que falló", "Motivo" })
                    h.Cell().Element(Cabecera).Text(t).Bold().FontSize(8.5f);
            });
            foreach (var f in det.Fallidas.Take(maximo))
            {
                table.Cell().Element(Celda).Text(f.Modulo).FontSize(8.5f);
                table.Cell().Element(Celda).Text(f.Nombre).FontSize(8.5f).Bold();
                table.Cell().Element(Celda).Text(string.IsNullOrWhiteSpace(f.Mensaje) ? "Sin mensaje de error registrado" : f.Mensaje).FontSize(8.5f);
            }
        });

        var mostradas = Math.Min(maximo, det.Fallidas.Count);
        if (det.TotalFallidas > mostradas)
            col.Item().Text($"… y {det.TotalFallidas - mostradas} pruebas fallidas más (el detalle completo está en el output.json).")
                .FontSize(8.5f).FontColor(Suave).Italic();
    }

    private static void TablaModulos(ColumnDescriptor col, DetalleRobotFramework det)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(3f); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); });
            table.Header(h =>
            {
                foreach (var t in new[] { "Módulo", "Pasaron", "Fallaron", "Omitidas" })
                    h.Cell().Element(Cabecera).Text(t).Bold().FontSize(8.5f);
            });
            foreach (var m in det.Modulos)
            {
                table.Cell().Element(Celda).Text(m.Nombre);
                table.Cell().Element(Celda).Text(m.Exitosas.ToString(EsPy));
                table.Cell().Element(Celda).Text(m.Fallidas.ToString(EsPy)).Bold().FontColor(m.Fallidas > 0 ? Rojo : Nav);
                table.Cell().Element(Celda).Text(m.Omitidas.ToString(EsPy));
            }
        });
    }

    private static void TablaLentas(ColumnDescriptor col, DetalleRobotFramework det)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(1.4f); c.RelativeColumn(3.6f); c.RelativeColumn(1); });
            table.Header(h =>
            {
                foreach (var t in new[] { "Módulo", "Prueba", "Duración" })
                    h.Cell().Element(Cabecera).Text(t).Bold().FontSize(8.5f);
            });
            foreach (var l in det.MasLentas)
            {
                table.Cell().Element(Celda).Text(l.Modulo).FontSize(8.5f);
                table.Cell().Element(Celda).Text(l.Nombre).FontSize(8.5f);
                table.Cell().Element(Celda).Text(Segundos(l.Segundos)).FontSize(8.5f);
            }
        });
    }

    // ── Perfil: Gerencia ──────────────────────────────────────────────────────────
    private static void SeccionesGerencia(ColumnDescriptor col, ReporteEjecutivoData d)
    {
        Seccion(col, "Qué significa para el negocio", c =>
        {
            var (titulo, detalle) = RiesgoNegocio(d.Recomendacion);
            c.Item().Text(t => { t.Span(titulo + ". ").Bold().FontColor(ColorRec(d.Recomendacion)); t.Span(detalle); });
            if (d.Criterios.Any(x => x.Veredicto is "no_cumple" or "revisar"))
                c.Item().Text("Indicadores que motivan la recomendación: " + string.Join("; ",
                    d.Criterios.Where(x => x.Veredicto is "no_cumple" or "revisar")
                        .Select(x => $"{NombreCriterio(x).ToLowerInvariant()} ({FormatoCriterio(x, x.Valor)}, {Exigencia(x)})")) + ".");
            if (d.Detalle is { TotalFallidas: > 0 } det)
                c.Item().Text($"Los fallos se concentran en: {ResumenModulosConFallas(det)}.");
        });

        Seccion(col, "Estado de la decisión", c =>
        {
            EstadoDecision(c, d);
            if (d.AdherenciaTotal > 0)
                c.Item().Text($"Como referencia, en este proyecto el equipo siguió la recomendación del sistema en {d.AdherenciaAlineadas} de {d.AdherenciaTotal} decisiones registradas.")
                    .FontColor(Suave);
        });

        if (d.Historial.Count > 1)
            Seccion(col, "Evolución reciente del proyecto", c => TablaHistorial(c, d, 5));

        Seccion(col, "Qué hacer ahora", c => Vinetas(c, Acciones(d).Take(4)));
    }

    // ── Perfil: Líder Técnico ─────────────────────────────────────────────────────
    private static void SeccionesTecnico(ColumnDescriptor col, ReporteEjecutivoData d)
    {
        Seccion(col, "Verificación criterio por criterio", c =>
        {
            TablaCriterios(c, d);
            c.Item().Text("Un criterio queda en «Revisar» cuando no alcanza el umbral pero está dentro de un margen de tolerancia de ±5 %. " +
                          "Si un solo criterio «No cumple», la recomendación es No desplegar; si ninguno falla pero alguno queda en «Revisar», la recomendación es Revisar.")
                .FontSize(8.5f).FontColor(Suave).Italic();
        });

        if (HayFallas(d))
            Seccion(col, "Pruebas que fallaron", c => TablaFallidas(c, d, 8));

        if (d.Anterior is { } ant)
            Seccion(col, "Cambios frente a la evaluación anterior", c =>
            {
                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(cc => { cc.RelativeColumn(2); cc.RelativeColumn(1.2f); cc.RelativeColumn(1.2f); cc.RelativeColumn(1.4f); });
                    table.Header(h =>
                    {
                        foreach (var t in new[] { "Indicador", $"Antes (v{ant.Version})", $"Ahora (v{d.Version})", "Variación" })
                            h.Cell().Element(Cabecera).Text(t).Bold().FontSize(8.5f);
                    });

                    void Fila(string nombre, decimal? antes, decimal? ahora)
                    {
                        table.Cell().Element(Celda).Text(nombre);
                        table.Cell().Element(Celda).Text(Pct(antes));
                        table.Cell().Element(Celda).Text(Pct(ahora)).Bold();
                        if (antes.HasValue && ahora.HasValue)
                        {
                            var dif = ahora.Value - antes.Value;
                            var signo = dif > 0 ? "+" : "";
                            table.Cell().Element(Celda).Text($"{signo}{dif.ToString("0.0", EsPy)} puntos")
                                .FontColor(dif > 0 ? Verde : dif < 0 ? Rojo : Suave).Bold();
                        }
                        else table.Cell().Element(Celda).Text("-");
                    }
                    Fila("Tasa de éxito", ant.TasaExito, d.TasaExito);
                    Fila("Cobertura de ejecución", ant.Cobertura, d.Cobertura);
                });
                c.Item().Text(t =>
                {
                    t.Span("Recomendación: antes ");
                    t.Span(TextoRecCorto(ant.Recomendacion)).Bold().FontColor(ColorRec(ant.Recomendacion));
                    t.Span(", ahora ");
                    t.Span(TextoRecCorto(d.Recomendacion)).Bold().FontColor(ColorRec(d.Recomendacion));
                    t.Span(".");
                });
            });

        Seccion(col, "Riesgos y acciones sugeridas", c => Vinetas(c, Acciones(d)));

        Seccion(col, "Decisión de despliegue", c =>
        {
            EstadoDecision(c, d);
            if (d.Decision == null && d.Recomendacion != "SIN_EVALUACION")
                c.Item().Text("Recordatorio: si la decisión contradice la recomendación del sistema, la justificación debe tener al menos 20 caracteres y la decisión queda marcada como excepción.")
                    .FontSize(8.5f).FontColor(Suave).Italic();
        });
    }

    // ── Perfil: Analista QA ───────────────────────────────────────────────────────
    private static void SeccionesQa(ColumnDescriptor col, ReporteEjecutivoData d)
    {
        Seccion(col, "Cómo se repartieron las pruebas", c =>
        {
            var exitosas = d.Exitosas ?? 0; var fallidas = d.Fallidas ?? 0; var omitidas = d.Omitidas ?? 0;
            var total = d.Total ?? (exitosas + fallidas + omitidas);
            if (total <= 0)
            {
                c.Item().Text("No hay conteo de pruebas para esta evaluación.");
                return;
            }

            c.Item().Row(r =>
            {
                if (exitosas > 0) r.RelativeItem(exitosas).Height(16).Background(Verde);
                if (fallidas > 0) r.RelativeItem(fallidas).Height(16).Background(Rojo);
                if (omitidas > 0) r.RelativeItem(omitidas).Height(16).Background("#9aa5b1");
            });

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cc => { cc.RelativeColumn(2.4f); cc.RelativeColumn(1); cc.RelativeColumn(1); });
                table.Header(h =>
                {
                    foreach (var t in new[] { "Resultado", "Pruebas", "% del plan" })
                        h.Cell().Element(Cabecera).Text(t).Bold().FontSize(8.5f);
                });
                void Fila(string nombre, int n, string color)
                {
                    table.Cell().Element(Celda).Text(nombre).Bold().FontColor(color);
                    table.Cell().Element(Celda).Text(n.ToString(EsPy));
                    table.Cell().Element(Celda).Text(Pct((decimal)n / total * 100));
                }
                Fila("Pasaron", exitosas, Verde);
                Fila("Fallaron", fallidas, Rojo);
                Fila("No se ejecutaron (omitidas)", omitidas, Suave);
                table.Cell().Element(Celda).Text("Total del plan").Bold();
                table.Cell().Element(Celda).Text(total.ToString(EsPy)).Bold();
                table.Cell().Element(Celda).Text("100,0 %").Bold();
            });
        });

        Seccion(col, "Cómo se calcularon las métricas", c =>
        {
            var total = d.Total;
            c.Item().Text(t =>
            {
                t.Span("Tasa de éxito = pasaron ÷ total. ").Bold();
                t.Span(total is int tt && tt > 0 ? $"{d.Exitosas} ÷ {tt} = {Pct(d.TasaExito)}." : "");
            });
            c.Item().Text(t =>
            {
                t.Span("Cobertura de ejecución = (pasaron + fallaron) ÷ total. ").Bold();
                t.Span(total is int tt && tt > 0 ? $"({d.Exitosas} + {d.Fallidas}) ÷ {tt} = {Pct(d.Cobertura)}." : "");
                t.Span(" Las pruebas omitidas no cuentan como ejecutadas.");
            });
            c.Item().Text("Por eso la cobertura nunca es menor que la tasa de éxito: la diferencia entre ambas es la tasa de fallo.")
                .FontColor(Suave).Italic();
        });

        Seccion(col, "Criterios evaluados", c => TablaCriterios(c, d));
        if (HayFallas(d))
            Seccion(col, "Pruebas que fallaron", c => TablaFallidas(c, d, 15));
        if (d.Detalle != null)
            Seccion(col, "Resultado por módulo", c => TablaModulos(c, d.Detalle));
        if (d.Detalle is { MasLentas.Count: > 0 })
            Seccion(col, "Pruebas más lentas", c => TablaLentas(c, d.Detalle));
        Seccion(col, "Qué corregir o revisar", c => Vinetas(c, Acciones(d)));

        if (d.Historial.Count > 1)
            Seccion(col, "Historial del proyecto", c => TablaHistorial(c, d, 8));

        Seccion(col, "Archivo de origen", c =>
        {
            c.Item().Text(t => { t.Span("Archivo: ").Bold(); t.Span(d.Archivo ?? "-"); });
            c.Item().Text(t => { t.Span("Cargado el: ").Bold(); t.Span(Fecha(d.FechaCarga) + (d.CargadoPor != null ? $" por {d.CargadoPor}" : "")); });
            if (!string.IsNullOrWhiteSpace(d.Observaciones))
                c.Item().Text(t => { t.Span("Observaciones: ").Bold(); t.Span(d.Observaciones!); });
        });
    }
}
