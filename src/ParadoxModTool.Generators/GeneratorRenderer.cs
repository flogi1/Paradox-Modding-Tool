using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace ParadoxModTool.Generators;

/// <summary>
/// Rendert Scriban-Vorlagen eines Pakets: die Paket-Funktionen (<c>functions.sbn</c>) stehen in jeder Vorlage bereit, unbekannte Namen sind
/// Fehler (<c>StrictVariables</c>). Fehler kommen als <see cref="GeneratorTemplateException"/> mit Vorlage und Zeile.
/// </summary>
public sealed class GeneratorRenderer
{
    private const string FunctionsName = "functions.sbn";

    private readonly Template? _functions;

    public GeneratorRenderer(GeneratorPackage package)
    {
        _functions = string.IsNullOrWhiteSpace(package.Functions) ? null : Parse(FunctionsName, package.Functions);
    }

    /// <summary>Rendert eine Vorlage; <paramref name="scope"/> ergänzt z. B. <c>item</c> (das aktuelle Element einer ForEach-Ausgabe).</summary>
    public string Render(string templateName, string templateText, ScriptObject model, ScriptObject? scope = null)
    {
        var template = Parse(templateName, templateText);
        var context = Context(model, scope);
        try
        {
            return template.Render(context);
        }
        catch (ScriptRuntimeException ex)
        {
            throw new GeneratorTemplateException(templateName, ex.Span.Start.Line + 1, ex.OriginalMessage);
        }
    }

    /// <summary>Kurzer Vorlagentext aus package.json (Pfad, Block, Markierung ...); Fehler nennen ihn selbst als Vorlage.</summary>
    public string RenderInline(string text, ScriptObject model, ScriptObject? scope = null) => Render(text, text, model, scope);

    /// <summary>Wertet einen Scriban-Ausdruck als Bedingung aus (Paket-Warnungen).</summary>
    public bool Evaluate(string expression, ScriptObject model) =>
        Render(expression, "{{ if (" + expression + ") }}1{{ end }}", model) == "1";

    private TemplateContext Context(ScriptObject model, ScriptObject? scope)
    {
        var context = new TemplateContext { StrictVariables = true, MemberRenamer = member => member.Name };
        context.PushGlobal(model);
        var locals = new ScriptObject();
        if (scope is not null)
        {
            locals.Import(scope);
        }

        context.PushGlobal(locals);
        if (_functions is not null)
        {
            try
            {
                _functions.Render(context);
            }
            catch (ScriptRuntimeException ex)
            {
                throw new GeneratorTemplateException(FunctionsName, ex.Span.Start.Line + 1, ex.OriginalMessage);
            }
        }

        return context;
    }

    private static Template Parse(string name, string text)
    {
        var template = Template.Parse(text, name);
        if (template.HasErrors)
        {
            var first = template.Messages.First(m => m.Type == ParserMessageType.Error);
            throw new GeneratorTemplateException(name, first.Span.Start.Line + 1, first.Message);
        }

        return template;
    }
}
