using System;
using System.Linq;
using HarmonyLib;

namespace CorsacCosmetics;

public static class ReactorCompat
{
    public const string ReactorID = "gg.reactor.api";
    private static Func<int, bool> ShowCredits { get; } = location => location == 0;

    public static void RegisterCredits()
    {
        try
        {
            var creditsType = AccessTools.TypeByName("Reactor.Utilities.ReactorCredits");
            var registerMethod = AccessTools
                .GetDeclaredMethods(creditsType)
                .Single(m => m.Name == "Register" && m.IsGenericMethodDefinition)
                ?.MakeGenericMethod(typeof(CorsacCosmeticsPlugin));

            if (registerMethod == null)
            {
                Error("Could not register credits with Reactor! The method was not found.");
                return;
            }

            var showCreditsType = registerMethod.GetParameters().First().ParameterType;
            var showCreditsDelegate = Delegate.CreateDelegate(showCreditsType, ShowCredits.Target, ShowCredits.Method);

            registerMethod.Invoke(null, [showCreditsDelegate]);
            Info("Registered credits with Reactor!");
        }
        catch (Exception e)
        {
            Error($"Could not register credits with Reactor! An exception was thrown:\n{e}");
        }
    }
}