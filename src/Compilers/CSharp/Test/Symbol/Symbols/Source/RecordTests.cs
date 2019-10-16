// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class RecordTests : CSharpTestBase
    {
        [Fact]
        public void Simple1()
        {
            var text =
@"
class A {
    record D();
}
";
            var comp = CreateCompilation(text);
            var global = comp.GlobalNamespace;
            var a = global.GetTypeMembers("A", 0).Single();
            var d = a.GetMembers("D")[0] as NamedTypeSymbol;
            var tmp = d.GetMembers();
            Assert.Equal(d.Locations[0], d.InstanceConstructors[0].Locations[0], EqualityComparer<Location>.Default);
        }

        [Fact]
        public void Duplicate()
        {
            var text =
@"
record D(int X);
record D(float Y);
";
            var comp = CreateCompilation(text);
            var diags = comp.GetDeclarationDiagnostics();
            Assert.Equal(1, diags.Count());

            var global = comp.GlobalNamespace;
            var d = global.GetTypeMembers("D", 0);
            Assert.Equal(2, d.Length);
        }

        [Fact]
        public void SimpleRecord()
        {
            var text =
@"record MyRec(int N);";

            var comp = CreateCompilation(text);
            var v = comp.GlobalNamespace.GetTypeMembers("MyRec", 0).Single();
            Assert.NotNull(v);
            Assert.Equal(SymbolKind.NamedType, v.Kind);
            Assert.Equal(TypeKind.Record, v.TypeKind);
            Assert.True(v.IsReferenceType);
            Assert.False(v.IsValueType);
            Assert.True(v.IsSealed);
            Assert.False(v.IsAbstract);
            Assert.Equal(0, v.Arity); // number of type parameters
            Assert.Equal(1, v.InstanceConstructors.Length);
            var ctor = v.InstanceConstructors[0];
            Assert.Equal(Accessibility.Public, ctor.DeclaredAccessibility);
            Assert.Equal(1, ctor.Parameters.Length);
            Assert.Equal(Accessibility.Internal, v.DeclaredAccessibility);
            Assert.Equal("System.Object", v.BaseType().ToTestDisplayString());
        }

        [Fact]
        public void Generics()
        {
            var text =
@"namespace NS
{
    internal record D<Q>(Q q);
}";

            var comp = CreateCompilation(text);
            var namespaceNS = comp.GlobalNamespace.GetMembers("NS").First() as NamespaceOrTypeSymbol;
            Assert.Equal(1, namespaceNS.GetTypeMembers().Length);

            var d = namespaceNS.GetTypeMembers("D").First();
            Assert.Equal(namespaceNS, d.ContainingSymbol);
            Assert.Equal(SymbolKind.NamedType, d.Kind);
            Assert.Equal(TypeKind.Record, d.TypeKind);
            Assert.Equal(Accessibility.Internal, d.DeclaredAccessibility);
            Assert.Equal(1, d.TypeParameters.Length);
            Assert.Equal("Q", d.TypeParameters[0].Name);
            var q = d.TypeParameters[0];
            Assert.Equal(q.ContainingSymbol, d);
            Assert.Equal(d.InstanceConstructors[0].Parameters[0].Type, q);

            // same as type parameter
            Assert.Equal(1, d.TypeArguments().Length);
        }

        [Fact]
        public void EscapedIdentifier()
        {
            var text = @"
record @out();
";
            var comp = CreateCompilation(Parse(text));
            var dout = (NamedTypeSymbol)comp.SourceModule.GlobalNamespace.GetMembers("out").Single();
            Assert.Equal("out", dout.Name);
            Assert.Equal("@out", dout.ToString());
        }

        [Fact]
        public void RecordsEverywhere()
        {
            var text = @"
record Intf(int x);
class C
{
    Intf I;
    Intf Method(Intf f1)
    {
        Intf i = f1;
        i = f1;
        I = i;
        i = I;
        object o1 = f1;
        f1 = i;
        return f1;
    }
}
";
            CreateCompilation(text).VerifyDiagnostics();
        }

        [Fact]
        public void RecordCreation()
        {
            var text = @"
namespace CSSample
{
    class Program
    {
        static void Main(string[] args)
        {
        }

        record R1();
        record R2(R1 x);
        record R3(int x);

        static R1 r1;
        static R2 r2;
        static R3 r3;

        static void F(Program p)
        {
            // Good cases
            r2 = new R2(r1);
            r1 = new R1();
            r2 = new R2(r1);
        }
    }
}
";
            CreateCompilation(text).VerifyDiagnostics(
                // (17,19): warning CS0169: The field 'CSSample.Program.r3' is never used
                //         static R3 r3;
                Diagnostic(ErrorCode.WRN_UnreferencedField, "r3").WithArguments("CSSample.Program.r3"));
        }
    }
}
