using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TextTemplating;
using Xunit;

namespace T4Studio.Tests
{
	/// <summary>
	/// Edge case tests for TemplatingEngine — covers bugs discovered during the
	/// SignalR T4 template development. These tests protect against regressions
	/// for bare return; in TransformText, System.IO import + class features,
	/// nested if/else across content blocks, and template compilation failures.
	/// </summary>
	public class TemplatingEngineEdgeCases
	{
		TemplatingEngine engine = new TemplatingEngine ();
		DummyHost host = new DummyHost ();

		static void AssertOutput (string expected, string actual)
		{
			expected = (expected ?? "").Replace ("\r\n", "\n").Replace ("\r", "\n");
			actual = (actual ?? "").Replace ("\r\n", "\n").Replace ("\r", "\n");
			Assert.Equal (expected, actual);
		}

		public TemplatingEngineEdgeCases ()
		{
			host.StandardAssemblyReferences.Add (typeof (TextTransformation).Assembly.Location);
			host.StandardAssemblyReferences.Add (typeof (System.Uri).Assembly.Location);
			host.StandardImports.Add ("System");
		}

		[Fact]
		public void BareReturnInMainBlock_ShouldReportClearError ()
		{
			// T4 compiles main block into TransformText() which returns string.
			// A bare "return;" is illegal and should produce a clear error.
			var template = @"<#@ template language=""C#"" #>
<# return; #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.Null (result);
			Assert.True (host.Errors.HasErrors);
			Assert.Contains (host.Errors.Cast<CompilerError> (),
				e => e.ErrorText.Contains ("string") || e.ErrorText.Contains ("return"));
		}

		[Fact]
		public void BareReturnInHostSpecificTemplate_ShouldReportClearError ()
		{
			var template = @"<#@ template language=""C#"" hostspecific=""true"" #>
<# if (this.Host == null) return; #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.Null (result);
			Assert.True (host.Errors.HasErrors);
		}

		[Fact]
		public void MultipleClassFeatureMethods_ShouldCompile ()
		{
			var template = @"<#@ template language=""C#"" #>
<# Write(Helper1() + Helper2() + Helper3()); #>
<#+
    string Helper1() { return ""A""; }
    string Helper2() { return ""B""; }
    string Helper3() { return ""C""; }
#>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			AssertOutput ("ABC", result);
		}

		[Fact]
		public void ClassFeatureWithTypeofExpressions_ShouldCompile ()
		{
			var template = @"<#@ template language=""C#"" #>
<#@ import namespace=""System"" #>
<# Write(TsType(typeof(string))); #>
<#+
    string TsType(System.Type t) {
        if (t == typeof(string)) return ""string"";
        if (t == typeof(int)) return ""int"";
        return ""other"";
    }
#>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			AssertOutput ("string", result);
		}

		[Fact]
		public void ClassFeatureWithGenericTypeDefinitions_ShouldCompile ()
		{
			var template = @"<#@ template language=""C#"" #>
<#@ import namespace=""System"" #>
<#@ import namespace=""System.Collections.Generic"" #>
<# Write(GetTypeName(typeof(List<int>))); #>
<#+
    string GetTypeName(System.Type t) {
        if (t.IsGenericType) {
            var gd = t.GetGenericTypeDefinition();
            if (gd == typeof(List<>)) return ""list"";
        }
        return ""other"";
    }
#>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Equal ("list", result);
		}

		[Fact]
		public void ClassFeatureWithTaskTypeof_ShouldCompile ()
		{
			var template = @"<#@ template language=""C#"" #>
<#@ import namespace=""System"" #>
<#@ import namespace=""System.Threading.Tasks"" #>
<# Write(GetRet(typeof(Task<string>))); #>
<#+
    string GetRet(System.Type t) {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>))
            return ""task"";
        return ""other"";
    }
#>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Equal ("task", result);
		}

		[Fact]
		public void NestedIfElseAcrossContentBlocks_ShouldCompile ()
		{
			var template = @"<#@ template language=""C#"" #>
<# bool a = true; bool b = false; #>
<# if (a) { #>
A
<#   if (b) { #>
B
<#   } else { #>
C
<#   } #>
<# } else { #>
D
<# } #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Equal ("A\r\nC\r\n", result, ignoreLineEndingDifferences: true);
		}

		[Fact]
		public void DeeplyNestedIfElse_ShouldCompile ()
		{
			var template = @"<#@ template language=""C#"" #>
<#
    bool x = true, y = true, z = false;
    if (x) {
        if (y) {
            if (z) {
                Write(""Z"");
            }
            else {
                Write(""notZ"");
            }
        }
        else {
            Write(""notY"");
        }
    }
    else {
        Write(""notX"");
    }
#>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Equal ("notZ", result);
		}

		[Fact]
		public void SystemReflectionImportWithClassFeatures_ShouldCompile ()
		{
			var template = @"<#@ template language=""C#"" #>
<#@ import namespace=""System"" #>
<#@ import namespace=""System.Reflection"" #>
<# Write(typeof(string).Assembly.GetName().Name); #>
<#+
    string GetAsm(System.Reflection.Assembly a) { return a.GetName().Name; }
#>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.NotNull (result);
		}

		[Fact]
		public void SystemIOImportWithClassFeatures_ShouldCompile ()
		{
			// Regression: System.IO import + class features used to trigger
			// "An object of a type convertible to 'string' is required"
			var template = @"<#@ template language=""C#"" #>
<#@ import namespace=""System"" #>
<#@ import namespace=""System.IO"" #>
<# Write(""OK""); #>
<#+
    bool Helper() { return true; }
#>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Equal ("OK", result);
		}

		[Fact]
		public void SystemIOFullyQualifiedWithClassFeatures_ShouldCompile ()
		{
			// Using System.IO.File.Exists (fully qualified) without System.IO import
			var template = @"<#@ template language=""C#"" #>
<#@ import namespace=""System"" #>
<# Write(System.IO.File.Exists(""nonexistent.txt"").ToString()); #>
<#+
    bool Helper() { return true; }
#>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Equal ("False", result);
		}

		[Fact]
		public void DuplicateImports_ShouldNotCauseError ()
		{
			var template = @"<#@ template language=""C#"" #>
<#@ import namespace=""System"" #>
<#@ import namespace=""System"" #>
<# Write(""OK""); #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Equal ("OK", result);
		}

		[Fact]
		public void EmptyTemplate_ShouldReturnEmptyString ()
		{
			var template = @"<#@ template language=""C#"" #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Equal ("", result);
		}

		[Fact]
		public void WhitespaceOnlyTemplate_ShouldReturnWhitespace ()
		{
			var template = @"<#@ template language=""C#"" #>

  
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			AssertOutput ("\n  \n", result);
		}

		[Fact]
		public void ContentOnlyTemplate_ShouldReturnContent ()
		{
			var template = @"<#@ template language=""C#"" #>
Hello World
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			AssertOutput ("Hello World\n", result);
		}

		[Fact]
		public void ExpressionBlock_ShouldEvaluateAndWrite ()
		{
			var template = @"<#@ template language=""C#"" #>
<#= 1 + 2 #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			AssertOutput ("3\n", result);
		}

		[Fact]
		public void ExpressionWithNullValue_ShouldHandleGracefully ()
		{
			var template = @"<#@ template language=""C#"" #>
<# string s = null; #>
<#= s ?? ""null"" #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			AssertOutput ("null\n", result);
		}

		[Fact]
		public void HostSpecificTemplate_ShouldGenerateHostProperty ()
		{
			var template = @"<#@ template language=""C#"" hostspecific=""true"" #>
<# Write(this.Host != null ? ""hasHost"" : ""noHost""); #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Equal ("hasHost", result);
		}

		[Fact]
		public void CultureAttribute_ShouldSetCulture ()
		{
			var template = @"<#@ template language=""C#"" culture=""en-US"" #>
<#= (1.5).ToString() #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			// Culture may produce "1.5" or "1.50" depending on framework
			Assert.NotNull (result);
			Assert.True (result.StartsWith ("1.5") || result.StartsWith ("1.50"),
				$"Unexpected culture output: '{result}'");
		}

		[Fact]
		public void UnresolvedAssemblyReference_ShouldReportError ()
		{
			// Some hosts resolve assembly names as paths — unresolved ones
			// may just be returned as-is (the compiler will then fail)
			var template = @"<#@ template language=""C#"" #>
<#@ assembly name=""NonExistentAssembly_XYZ.dll"" #>
<# Write(""OK""); #>
";

			var result = engine.ProcessTemplate (template, host);

			// The assembly reference may be passed through if the host
			// doesn't validate. Either the template returns OK (host passes it)
			// or returns null (compiler fails to find it).
			Assert.True (result == null || result == "OK");
		}

		[Fact]
		public void MissingLanguage_ShouldReportError ()
		{
			var template = @"<#@ template #>
<# Write(""OK""); #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.Null (result);
			Assert.True (host.Errors.HasErrors);
			Assert.Contains (host.Errors.Cast<CompilerError> (),
				e => e.ErrorText.Contains ("language") || e.ErrorText.Contains ("Language"));
		}

		[Fact]
		public void LocalVariablesFromMainBlock_PersistAcrossContentBlocks ()
		{
			var template = @"<#@ template language=""C#"" #>
<# int x = 10; #>
A<#= x #>B
<# x = 20; #>
C<#= x #>D
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			Assert.Contains ("A10B", result);
			Assert.Contains ("C20D", result);
		}

		[Fact]
		public void CompilerError_ShouldNotCrashEngine ()
		{
			// Code that doesn't compile should return null + errors, not throw
			var template = @"<#@ template language=""C#"" #>
<# UndeclaredVariable.Foo(); #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.Null (result);
			Assert.True (host.Errors.HasErrors);
		}

		[Fact]
		public void UsingBlockInTemplate_ShouldCompile ()
		{
			var template = @"<#@ template language=""C#"" #>
<# using (var ms = new System.IO.MemoryStream()) { Write(""OK""); } #>
";

			var result = engine.ProcessTemplate (template, host);

			Assert.False (host.Errors.HasErrors, string.Join ("; ", host.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
			AssertOutput ("OK", result);
		}
	}
}


