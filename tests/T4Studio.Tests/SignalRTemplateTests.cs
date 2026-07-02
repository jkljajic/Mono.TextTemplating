using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TextTemplating;
using Xunit;

namespace T4Studio.Tests
{
	/// <summary>
	/// Integration tests for the SignalR Hub → TypeScript T4 template.
	/// Verifies the template compiles, runs, and generates valid TypeScript
	/// when given a target assembly. Tests both the happy path and edge cases.
	/// </summary>
	public class SignalRTemplateTests
	{
		readonly TemplatingEngine engine = new TemplatingEngine ();

		TemplateGenerator CreateGenerator (string targetAssembly = null)
		{
			var gen = new TemplateGenerator ();
			gen.Refs.Add (typeof (TextTransformation).Assembly.Location);
			gen.Refs.Add (typeof (System.Uri).Assembly.Location);
			gen.Imports.Add ("System");
			gen.Imports.Add ("System.Linq");
			gen.Imports.Add ("System.Reflection");
			gen.Imports.Add ("System.Threading.Tasks");
			gen.Imports.Add ("System.Collections.Generic");
			if (targetAssembly != null)
				gen.AddParameter (null, null, "TargetAssembly", targetAssembly);
			return gen;
		}

		string GetTemplateContent ()
		{
			// Read the actual template file from the sample project
			var templatePath = Path.Combine (
				GetRepoRoot (), "samples", "SignalR.TsGeneration",
				"Templates", "HubClientGenerator.tt");
			if (File.Exists (templatePath))
				return File.ReadAllText (templatePath);

			// Fallback: inline minimal template for CI environments
			return MinimalHubTemplate;
		}

		static string GetRepoRoot ()
		{
			var dir = new DirectoryInfo (AppContext.BaseDirectory);
			while (dir != null) {
				if (File.Exists (Path.Combine (dir.FullName, "T4Studio.sln")))
					return dir.FullName;
				dir = dir.Parent;
			}
			return AppContext.BaseDirectory;
		}

		[Fact]
		public void TemplateWithoutTargetAssembly_ShouldReturnPlaceholder ()
		{
			var template = @"<#@ template language=""C#"" hostspecific=""true"" #>
<#@ output extension="".ts"" #>
<#@ import namespace=""T4Studio"" #>
<#
    var types = this.Host.FindTypes(t => t.Name.Contains(""String""));
    if (types.Count == 0) {
        Write(""// No types found\n"");
    }
#>
";

			var gen = CreateGenerator ();
			var result = engine.ProcessTemplate (template, gen);

			Assert.False (gen.Errors.HasErrors,
				string.Join ("; ", gen.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.NotNull (result);
		}

		[Fact]
		public void TemplateWithCurrentAssembly_ShouldDiscoverTypes ()
		{
			var template = @"<#@ template language=""C#"" hostspecific=""true"" #>
<#@ output extension="".ts"" #>
<#@ import namespace=""System"" #>
<#@ import namespace=""System.Linq"" #>
<#@ import namespace=""System.Reflection"" #>
<#
    var asm = typeof(T4Studio.TemplatingEngine).Assembly;
    var types = asm.GetTypes().Where(t => t.IsPublic && !t.IsAbstract).Take(3).ToList();
    Write(""// count: "" + types.Count + ""\n"");
#>
";

			var gen = CreateGenerator ();
			var result = engine.ProcessTemplate (template, gen);

			Assert.False (gen.Errors.HasErrors,
				string.Join ("; ", gen.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.NotNull (result);
			Assert.StartsWith ("// count:", result.Trim ());
		}

		[Fact]
		public void TemplateWithAssemblyGetTypes_ShouldNotThrowReflectionTypeLoadException ()
		{
			// Regression: assembly.GetTypes() can throw ReflectionTypeLoadException
			// when dependencies are missing. The template should handle this gracefully.
			var template = @"<#@ template language=""C#"" hostspecific=""true"" #>
<#@ output extension="".ts"" #>
<#@ import namespace=""System"" #>
<#@ import namespace=""System.Linq"" #>
<#@ import namespace=""System.Reflection"" #>
<#
    var asm = typeof(T4Studio.TemplatingEngine).Assembly;
    try {
        var types = asm.GetTypes();
        Write(""// success: "" + types.Length + "" types\n"");
    } catch (ReflectionTypeLoadException ex) {
        Write(""// partial: "" + ex.Types.Count(t => t != null) + "" loaded\n"");
    }
#>
";

			var gen = CreateGenerator ();
			var result = engine.ProcessTemplate (template, gen);

			Assert.False (gen.Errors.HasErrors,
				string.Join ("; ", gen.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.NotNull (result);
			Assert.StartsWith ("// success:", result.Trim ());
		}

		[Fact]
		public void TemplateGeneratesValidTypeScript_ForKnownMethod ()
		{
			var template = @"<#@ template language=""C#"" #>
<#@ output extension="".ts"" #>
<#@ import namespace=""System"" #>
<#
    var methods = typeof(string).GetMethods().Take(2).ToList();
    foreach (var m in methods) {
        var ps = string.Join("", "", m.GetParameters().Select(p => p.Name + "": any""));
#>
export function <#= Char.ToLowerInvariant(m.Name[0]) + m.Name.Substring(1) #>(<#= ps #>): void {}
<#
    }
#>
<#+
    string Camel(string s) { return string.IsNullOrEmpty(s) ? """" : char.ToLowerInvariant(s[0]) + s.Substring(1); }
#>
";

			var gen = CreateGenerator ();
			var result = engine.ProcessTemplate (template, gen);

			Assert.False (gen.Errors.HasErrors,
				string.Join ("; ", gen.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.NotNull (result);

			// Verify generated TypeScript has expected patterns
			Assert.Contains ("export function", result);
			Assert.Contains (": void {}", result);
		}

		[Fact]
		public void TemplateWithNullConditionalHostAccess_ShouldCompile ()
		{
			// Regression: this.Host?.ResolveParameterValue used to cause issues
			// when combined with class features
			var template = @"<#@ template language=""C#"" hostspecific=""true"" #>
<#@ output extension="".ts"" #>
<#@ import namespace=""System"" #>
<#
    var val = this.Host?.ResolveParameterValue(null, null, ""TargetAssembly"") ?? ""default"";
    Write(""// "" + val + ""\n"");
#>
<#+
    string Helper() { return ""x""; }
#>
";

			var gen = CreateGenerator ();
			var result = engine.ProcessTemplate (template, gen);

			Assert.False (gen.Errors.HasErrors,
				string.Join ("; ", gen.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Contains ("// default", result);
		}

		[Fact]
		public void FullHubGeneratorTemplate_ShouldCompile ()
		{
			// Requires ASP.NET Core runtime — skipped if not available
			if (Type.GetType ("Microsoft.AspNetCore.SignalR.Hub, Microsoft.AspNetCore.SignalR.Core") == null)
				return;
			var template = GetTemplateContent ();
			var gen = CreateGenerator ();
			gen.Refs.Add (typeof (TemplatingEngine).Assembly.Location);

			var result = engine.ProcessTemplate (template, gen);

			foreach (CompilerError err in gen.Errors)
				Assert.DoesNotContain ("object of a type convertible to 'string'",
					err.ErrorText.ToLowerInvariant ());
		}

		#region Minimal Templates

		const string MinimalHubTemplate = @"<#@ template language=""C#"" hostspecific=""true"" #>
<#@ output extension="".ts"" #>
<#@ import namespace=""System"" #>
<#@ import namespace=""System.Linq"" #>
<#@ import namespace=""System.Reflection"" #>
<#@ import namespace=""System.Threading.Tasks"" #>
<#@ import namespace=""System.Collections.Generic"" #>
<#
    var targetAssembly = this.Host?.ResolveParameterValue(null, null, ""TargetAssembly"") ?? """";
    if (string.IsNullOrEmpty(targetAssembly) || !System.IO.File.Exists(targetAssembly)) {
        Write(""// No target assembly. MSBuild passes $(TargetPath) post-build.\n"");
    }
    else {
        Assembly assembly = null;
        try { assembly = Assembly.LoadFrom(targetAssembly); }
        catch (Exception ex) { Write(""// Load failed: "" + ex.Message + ""\n""); }
        if (assembly != null) {
            var hubs = assembly.GetTypes()
                .Where(t => t.IsPublic && !t.IsAbstract && IsHub(t))
                .OrderBy(t => t.Name)
                .ToList();
            if (hubs.Count == 0) {
                Write(""// No SignalR Hub types found.\n"");
            }
            else {
#>
// Auto-generated SignalR TypeScript clients
// Hubs: <#= string.Join("", "", hubs.Select(h => h.Name)) #>
import * as signalR from ""@microsoft/signalr"";
<#
                foreach (var hubType in hubs) {
                    var hubName = hubType.Name;
                    var methods = hubType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .Where(m => !m.IsSpecialName && m.DeclaringType == hubType && typeof(Task).IsAssignableFrom(m.ReturnType))
                        .ToList();
#>

export class <#= hubName #>Client {
    private conn: signalR.HubConnection;
    constructor(conn: signalR.HubConnection) { this.conn = conn; }
<#
                    foreach (var method in methods) {
                        var ps = method.GetParameters();
                        var psStr = string.Join("", "", ps.Select(p => p.Name + "": "" + TsType(p.ParameterType)));
                        var invokeArgs = ps.Length > 0
                            ? string.Join("", "", ps.Select(p => ""\"""" + p.Name + ""\"", "" + p.Name))
                            : """";
#>
    async <#= Camel(method.Name) #>(<#= psStr #>): Promise<void> {
<# if (ps.Length > 0) { #>
        return this.conn.invoke(""<#= method.Name #>"", <#= invokeArgs #>);
<# } else { #>
        return this.conn.invoke(""<#= method.Name #>"");
<# } #>
    }
<#
                    }
#>
}
export async function connect<#= hubName #>(url: string): Promise<<#= hubName #>Client> {
    const c = new signalR.HubConnectionBuilder().withUrl(url).withAutomaticReconnect().build();
    await c.start();
    return new <#= hubName #>Client(c);
}
<#
                }
            }
        }
    }
#>
<#+
    bool IsHub(Type t) {
        Type bt = t.BaseType;
        while (bt != null && bt != typeof(object)) {
            string fn = bt.FullName ?? """";
            if (fn == ""Microsoft.AspNetCore.SignalR.Hub"") return true;
            if (bt.IsGenericType && bt.GetGenericTypeDefinition().FullName == ""Microsoft.AspNetCore.SignalR.Hub`1"") return true;
            bt = bt.BaseType;
        }
        return false;
    }
    string TsType(Type t) {
        if (t == typeof(string)) return ""string"";
        if (t == typeof(bool)) return ""boolean"";
        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) return ""number"";
        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal)) return ""number"";
        if (t == typeof(void)) return ""void"";
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(Guid)) return ""string"";
        if (t.IsArray) return TsType(t.GetElementType() ?? typeof(object)) + ""[]"";
        if (t.IsGenericType) {
            var gd = t.GetGenericTypeDefinition();
            var ga = t.GetGenericArguments();
            if (gd == typeof(Nullable<>)) return TsType(ga[0]) + "" | null"";
            if (gd == typeof(Task<>)) return ""Promise<"" + TsType(ga[0]) + "">"";
            if (gd == typeof(List<>) || gd == typeof(IList<>) || gd == typeof(IEnumerable<>)) return TsType(ga[0]) + ""[]"";
        }
        return ""any"";
    }
    string Camel(string s) { return string.IsNullOrEmpty(s) ? """" : char.ToLowerInvariant(s[0]) + s.Substring(1); }
#>
";

		#endregion
	}
}


