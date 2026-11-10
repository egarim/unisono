using System;
using Foundation;
using ObjCRuntime;

namespace Unisono.AppIntents
{
	/// <summary>Completion block shared by every bridge call: (resultJson, errorMessage), one of them null.</summary>
	delegate void UnisonoCompletion([NullAllowed] string result, [NullAllowed] string error);

	/// <summary>
	/// The protocol the C# side implements (see RegistryHost in Unisono.AppIntents).
	/// Mirrors UnisonoHost in native/UnisonoBridge.swift.
	/// </summary>
	[Protocol, Model]
	[BaseType(typeof(NSObject))]
	interface UnisonoHost
	{
		[Abstract, Export("invoke:argumentsJSON:completion:")]
		void Invoke(string tool, string argumentsJson, UnisonoCompletion completion);

		[Abstract, Export("entities:ids:completion:")]
		void Entities(string type, string[] ids, UnisonoCompletion completion);

		[Abstract, Export("searchEntities:text:completion:")]
		void SearchEntities(string type, string text, UnisonoCompletion completion);

		[Abstract, Export("suggestedEntities:completion:")]
		void SuggestedEntities(string type, UnisonoCompletion completion);
	}

	/// <summary>Singleton on the Swift side that holds the host.</summary>
	[BaseType(typeof(NSObject))]
	[DisableDefaultCtor]
	interface UnisonoBridge
	{
		[Static, Export("shared")]
		UnisonoBridge Shared { get; }

		[NullAllowed, Export("host", ArgumentSemantic.Weak)]
		UnisonoHost Host { get; set; }
	}
}
