using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Belen.Architecture.Tests
{
    // Enforces the hexagonal rule: Domain/Application layers must not depend on Unity
    // engine *services*. Value-type math (Vector3, Mathf, Quaternion, Matrix4x4) is allowed,
    // but no UnityEngine.Object-derived types (MonoBehaviour, ScriptableObject, Transform,
    // Camera, AudioSource, ...) may appear as a type or a field in these assemblies.
    //
    // Note: this reflection-based guard catches type/field leakage. A stray static call
    // (e.g. Time.deltaTime) inside a method body is not caught here — that's covered by
    // folder/asmdef discipline and review. Upgrade to a Mono.Cecil IL scan if needed.
    public class ArchitecturePurityTests
    {
        private static readonly string[] PureAssemblies =
        {
            "Belen.HeadTracking.Domain",
            "Belen.HeadTracking.Application",
            "Belen.Presentation.Domain",
            "Belen.Presentation.Application",
            "Belen.Narrative.Domain",
            "Belen.Narrative.Application",
        };

        [Test]
        public void PureLayers_DoNotDeclareUnityObjectTypes()
        {
            foreach (var asmName in PureAssemblies)
            {
                var asm = Find(asmName);
                foreach (var t in asm.GetTypes())
                {
                    Assert.IsFalse(
                        typeof(UnityEngine.Object).IsAssignableFrom(t),
                        $"{asmName}: type '{t.FullName}' derives from UnityEngine.Object. " +
                        "MonoBehaviour/ScriptableObject/Component belong in Infrastructure.");
                }
            }
        }

        [Test]
        public void PureLayers_DoNotHoldUnityObjectFields()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static;
            foreach (var asmName in PureAssemblies)
            {
                var asm = Find(asmName);
                foreach (var t in asm.GetTypes())
                {
                    foreach (var f in t.GetFields(flags))
                    {
                        Assert.IsFalse(
                            typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType),
                            $"{asmName}: field '{t.FullName}.{f.Name}' is a UnityEngine.Object " +
                            $"('{f.FieldType.Name}'). Engine references belong in Infrastructure.");
                    }
                }
            }
        }

        private static Assembly Find(string name)
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name);
            Assert.IsNotNull(asm, $"Assembly '{name}' is not loaded.");
            return asm;
        }
    }
}
