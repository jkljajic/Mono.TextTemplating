using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Mono.TextTemplating
{
	/// <summary>
	/// Static helpers for .tt template code — type discovery, reflection, DI resolution.
	/// Eliminates boilerplate reflection from templates. Use directly or via this.Host extensions.
	/// </summary>
	public static class TemplateDiscovery
	{
		#region Type Discovery

		/// <summary>Finds all public, non-abstract types in loaded assemblies that inherit from baseType.</summary>
		public static List<Type> FindSubclassesOf (Type baseType)
			=> FindTypes (t => IsSubclassOf (t, baseType, baseType.IsGenericType));

		/// <summary>Finds all public, non-abstract types implementing the given interface.</summary>
		public static List<Type> FindImplementationsOf (Type interfaceType)
		{
			if (!interfaceType.IsInterface)
				throw new ArgumentException (interfaceType.Name + " is not an interface", nameof (interfaceType));
			return FindTypes (t => t.GetInterfaces ().Any (i =>
				i == interfaceType || (i.IsGenericType && i.GetGenericTypeDefinition () == interfaceType)));
		}

		/// <summary>Finds all types decorated with a specific attribute in loaded assemblies.</summary>
		public static List<Type> FindTypesWithAttribute<TAttr> () where TAttr : Attribute
			=> FindTypes (t => t.GetCustomAttribute<TAttr> () != null);

		/// <summary>Filter types by predicate across all loaded assemblies.</summary>
		public static List<Type> FindTypes (Func<Type, bool> predicate)
		{
			var results = new List<Type> ();
			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies ()) {
				try {
					foreach (var t in asm.GetTypes ())
						if (t.IsPublic && !t.IsAbstract && predicate (t))
							results.Add (t);
				} catch (ReflectionTypeLoadException ex) {
					foreach (var t in ex.Types)
						if (t != null && t.IsPublic && !t.IsAbstract && predicate (t))
							results.Add (t);
				} catch { }
			}
			return results;
		}

		/// <summary>Finds assemblies matching a name pattern (wildcard: MyApp.*).</summary>
		public static List<Assembly> FindAssemblies (string namePattern)
		{
			var results = new List<Assembly> ();
			var pattern = namePattern.Replace ("*", "");
			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies ()) {
				var name = asm.GetName ().Name ?? "";
				if (name.StartsWith (pattern, StringComparison.OrdinalIgnoreCase))
					results.Add (asm);
			}
			return results;
		}

		static bool IsSubclassOf (Type t, Type baseType, bool isGeneric)
		{
			var bt = t.BaseType;
			while (bt != null && bt != typeof (object)) {
				if (bt == baseType) return true;
				if (isGeneric && bt.IsGenericType && bt.GetGenericTypeDefinition () == baseType) return true;
				bt = bt.BaseType;
			}
			return false;
		}

		#endregion

		#region Member Discovery

		/// <summary>Gets public, declared-only instance methods of a type.</summary>
		public static List<MethodInfo> GetMethods (Type type)
			=> type.GetMethods (BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
				.Where (m => !m.IsSpecialName && m.DeclaringType == type).ToList ();

		/// <summary>Gets methods returning Task/ValueTask, excluding CancellationToken params.</summary>
		public static List<MethodInfo> GetTaskMethods (Type type)
			=> GetMethods (type)
				.Where (m => (typeof (Task).IsAssignableFrom (m.ReturnType) ||
				              (m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition () == typeof (ValueTask<>)))
				    && !m.GetParameters ().Any (p => p.ParameterType == typeof (System.Threading.CancellationToken)))
				.ToList ();

		/// <summary>Gets public, declared-only instance properties.</summary>
		public static List<PropertyInfo> GetProperties (Type type)
			=> type.GetProperties (BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
				.Where (p => p.DeclaringType == type).ToList ();

		/// <summary>Gets public, declared-only instance fields.</summary>
		public static List<FieldInfo> GetFields (Type type)
			=> type.GetFields (BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
				.Where (f => f.DeclaringType == type).ToList ();

		/// <summary>Gets members with a specific attribute.</summary>
		public static List<TMember> GetMembersWithAttribute<TAttr, TMember> (Type type,
			Func<Type, List<TMember>> getMembers) where TAttr : Attribute where TMember : MemberInfo
			=> getMembers (type).Where (m => m.GetCustomAttribute<TAttr> () != null).ToList ();

		/// <summary>Gets all custom attributes of a given type on a member.</summary>
		public static List<TAttr> GetAttributes<TAttr> (MemberInfo member) where TAttr : Attribute
			=> member.GetCustomAttributes<TAttr> ().ToList ();

		/// <summary>Gets the generic interface argument. e.g. Hub&lt;IChatClient&gt; → IChatClient.</summary>
		public static Type GetGenericInterfaceArg (Type type, Type interfaceType)
		{
			foreach (var iface in type.GetInterfaces ()) {
				if (iface.IsGenericType && iface.GetGenericTypeDefinition () == interfaceType)
					return iface.GetGenericArguments ()[0];
			}
			// Also check base type chain
			var bt = type.BaseType;
			while (bt != null && bt != typeof (object)) {
				if (bt.IsGenericType && bt.GetGenericTypeDefinition () == interfaceType)
					return bt.GetGenericArguments ()[0];
				bt = bt.BaseType;
			}
			return null;
		}

		#endregion

		#region Type Info

		/// <summary>Walks the base type chain and returns all base types.</summary>
		public static List<Type> GetBaseTypes (Type t)
			=> Walk (t.BaseType).ToList ();

		static IEnumerable<Type> Walk (Type t)
		{
			while (t != null && t != typeof (object)) {
				yield return t;
				t = t.BaseType;
			}
		}

		/// <summary>Returns true if t1 inherits from t2.</summary>
		public static bool InheritsFrom (Type t, Type baseType)
			=> IsSubclassOf (t, baseType, baseType.IsGenericType);

		/// <summary>Safely loads an assembly from a file path.</summary>
		public static Assembly LoadAssembly (string path)
		{
			try { return Assembly.LoadFrom (path); }
			catch { return null; }
		}

		#endregion
	}

	/// <summary>
	/// Extension methods on ITextTemplatingEngineHost — clean .tt template syntax.
	/// Usage: var types = this.Host.FindSubclassesOf(typeof(MyBase));
	///        var iface = this.Host.GetGenericInterfaceArg(myType, typeof(Hub<>));
	///        var svc   = this.Host.Resolve&lt;IMyService&gt;();
	/// </summary>
	public static class HostDiscoveryExtensions
	{
		// Type discovery
		public static List<Type> FindSubclassesOf (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Type b) => TemplateDiscovery.FindSubclassesOf (b);
		public static List<Type> FindImplementationsOf (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Type i) => TemplateDiscovery.FindImplementationsOf (i);
		public static List<Type> FindTypesWithAttribute<T> (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h) where T : Attribute => TemplateDiscovery.FindTypesWithAttribute<T> ();
		public static List<Type> FindTypes (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Func<Type, bool> p) => TemplateDiscovery.FindTypes (p);
		public static List<Assembly> FindAssemblies (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, string pat) => TemplateDiscovery.FindAssemblies (pat);

		// Member discovery
		public static List<MethodInfo> GetMethods (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Type t) => TemplateDiscovery.GetMethods (t);
		public static List<MethodInfo> GetTaskMethods (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Type t) => TemplateDiscovery.GetTaskMethods (t);
		public static List<PropertyInfo> GetProperties (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Type t) => TemplateDiscovery.GetProperties (t);
		public static List<FieldInfo> GetFields (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Type t) => TemplateDiscovery.GetFields (t);
		public static List<TMember> GetMembersWithAttribute<TAttr, TMember> (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Type t, Func<Type, List<TMember>> get) where TAttr : Attribute where TMember : MemberInfo => TemplateDiscovery.GetMembersWithAttribute<TAttr, TMember> (t, get);
		public static List<TAttr> GetAttributes<TAttr> (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, MemberInfo m) where TAttr : Attribute => TemplateDiscovery.GetAttributes<TAttr> (m);

		// Type info
		public static Type GetGenericInterfaceArg (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Type t, Type i) => TemplateDiscovery.GetGenericInterfaceArg (t, i);
		public static List<Type> GetBaseTypes (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Type t) => TemplateDiscovery.GetBaseTypes (t);
		public static bool InheritsFrom (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, Type t, Type b) => TemplateDiscovery.InheritsFrom (t, b);
		public static Assembly LoadAssembly (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h, string p) => TemplateDiscovery.LoadAssembly (p);

		// DI
		public static T Resolve<T> (this Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost h) where T : class {
			var sp = h.GetHostOption ("ServiceProvider") as IServiceProvider;
			return sp?.GetService (typeof (T)) as T;
		}
	}
}
