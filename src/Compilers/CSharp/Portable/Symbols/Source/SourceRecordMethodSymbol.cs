using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Cci;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal static class SourceRecordMethodSymbol
    {
        internal static void AddRecordMembers(
            SourceMemberContainerTypeSymbol recordType,
            ArrayBuilder<Symbol> symbols,
            RecordDeclarationSyntax syntax,
            DiagnosticBag diagnostics)
        {
            _ = recordType.DeclaringCompilation;
            var constructor = new Constructor(recordType, syntax, diagnostics);
            symbols.Add(constructor);

            var fieldsCount = constructor.ParameterCount;

            for (int fieldIndex = 0; fieldIndex < fieldsCount; fieldIndex++)
            {
                var parameter = constructor.Parameters[fieldIndex];
                var type = constructor.ParameterTypesWithAnnotations[fieldIndex];
                var locations = parameter.Locations;
                var property = new Property(recordType, type, locations, fieldIndex, parameter.Name);
                symbols.Add(property);
                symbols.Add(property.BackingField);
                symbols.Add(property.GetMethod);
            }

            symbols.Add(new EqualsMethodSymbol(recordType));
            symbols.Add(new GetHashCodeMethodSymbol(recordType));
            symbols.Add(new ToStringMethodSymbol(recordType));
            symbols.Add(new DeconstructMethodSymbol(recordType, syntax, diagnostics));
        }

        private sealed class Property : PropertySymbol
        {
            public Property(
                SourceMemberContainerTypeSymbol container,
                TypeWithAnnotations fieldTypeWithAnnotations,
                ImmutableArray<Location> locations,
                int index,
                string name)
            {
                Debug.Assert((object)container != null);
                Debug.Assert(fieldTypeWithAnnotations.HasType);
                Debug.Assert(index >= 0);
                Debug.Assert(name != null);

                ContainingType = container;
                TypeWithAnnotations = fieldTypeWithAnnotations;
                MemberIndexOpt = index;
                Name = name;
                Locations = locations;
                GetMethod = new PropertyGetAccessor(this);
                BackingField = new BackingFieldSymbol(this);
            }

            internal override int? MemberIndexOpt { get; }
            public FieldSymbol BackingField { get; }
            public override string Name { get; }
            public override RefKind RefKind => RefKind.None;
            public override TypeWithAnnotations TypeWithAnnotations { get; }
            public override ImmutableArray<CustomModifier> RefCustomModifiers => ImmutableArray<CustomModifier>.Empty;
            public override ImmutableArray<ParameterSymbol> Parameters => ImmutableArray<ParameterSymbol>.Empty;
            public override bool IsIndexer => false;
            public override MethodSymbol GetMethod { get; }
            public override MethodSymbol SetMethod => null;
            public override ImmutableArray<PropertySymbol> ExplicitInterfaceImplementations => ImmutableArray<PropertySymbol>.Empty;
            public override NamedTypeSymbol ContainingType { get; }
            public override Symbol ContainingSymbol => ContainingType;
            public override ImmutableArray<Location> Locations { get; }
            public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences => GetDeclaringSyntaxReferenceHelper<AnonymousObjectMemberDeclaratorSyntax>(Locations);
            public override Accessibility DeclaredAccessibility => Accessibility.Public;
            public override bool IsStatic => false;
            public override bool IsVirtual => false;
            public override bool IsOverride => false;
            public override bool IsAbstract => false;
            public override bool IsSealed => false;
            public override bool IsExtern => false;
            internal override bool HasSpecialName => false;
            internal override CallingConvention CallingConvention => CallingConvention.HasThis;
            internal override bool MustCallMethodsDirectly => false;
            internal override ObsoleteAttributeData ObsoleteAttributeData => null;
        }

        private sealed class PropertyGetAccessor : SynthesizedMethodBase
        {
            private readonly Property _property;

            internal PropertyGetAccessor(Property property)
                : base(property.ContainingType, SourcePropertyAccessorSymbol.GetAccessorName(property.Name, getNotSet: true, isWinMdOutput: false))
            {
                _property = property;
            }

            public override MethodKind MethodKind => MethodKind.PropertyGet;
            public override bool ReturnsVoid => false;
            public override RefKind RefKind => RefKind.None;
            public override TypeWithAnnotations ReturnTypeWithAnnotations => _property.TypeWithAnnotations;
            public override ImmutableArray<ParameterSymbol> Parameters => ImmutableArray<ParameterSymbol>.Empty;
            public override Symbol AssociatedSymbol => _property;
            public override ImmutableArray<Location> Locations => _property.Locations;
            public override bool IsOverride => false;
            internal sealed override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false) => false;
            internal override bool IsMetadataFinal => false;
            internal override bool SynthesizesLoweredBoundBody => true;
            internal override bool HasSpecialName => true;

            internal override void GenerateMethodBody(TypeCompilationState compilationState, DiagnosticBag diagnostics)
            {
                //  Method body:
                //
                //  {
                //      return this.backingField;
                //  }

                var F = CreateBoundNodeFactory(compilationState, diagnostics);
                F.CloseMethod(F.Block(F.Return(F.Field(F.This(), _property.BackingField))));
            }
        }

        private sealed class BackingFieldSymbol : FieldSymbol
        {
            private readonly PropertySymbol _property;

            public BackingFieldSymbol(PropertySymbol property)
            {
                Debug.Assert((object)property != null);
                _property = property;
            }

            public override string Name => GeneratedNames.MakeBackingFieldName(_property.Name);
            public override FlowAnalysisAnnotations FlowAnalysisAnnotations => FlowAnalysisAnnotations.None;
            internal override bool HasSpecialName => false;
            internal override bool HasRuntimeSpecialName => false;
            internal override bool IsNotSerialized => false;
            internal override MarshalPseudoCustomAttributeData MarshallingInformation => null;
            internal override int? TypeLayoutOffset => null;
            public override Symbol AssociatedSymbol => _property;
            public override bool IsReadOnly => true;
            public override bool IsVolatile => false;
            public override bool IsConst => false;
            internal sealed override ObsoleteAttributeData ObsoleteAttributeData => null;
            internal override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress, bool earlyDecodingWellKnownAttributes) => null;
            public override Symbol ContainingSymbol => _property.ContainingType;
            public override NamedTypeSymbol ContainingType => _property.ContainingType;
            public override ImmutableArray<Location> Locations => ImmutableArray<Location>.Empty;
            public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences => ImmutableArray<SyntaxReference>.Empty;
            public override Accessibility DeclaredAccessibility => Accessibility.Private;
            public override bool IsStatic => false;
            public override bool IsImplicitlyDeclared => true;

            internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) =>
                _property.TypeWithAnnotations;

            internal override void AddSynthesizedAttributes(PEModuleBuilder moduleBuilder, ref ArrayBuilder<SynthesizedAttributeData> attributes)
            {
                base.AddSynthesizedAttributes(moduleBuilder, ref attributes);

                var compilation = moduleBuilder.Compilation;

                AddSynthesizedAttribute(ref attributes,
                    compilation.TrySynthesizeAttribute(
                        WellKnownMember.System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor));

                AddSynthesizedAttribute(ref attributes,
                    compilation.SynthesizeDebuggerBrowsableNeverAttribute());
            }
        }

        private sealed class Constructor : SynthesizedMethodBase
        {
            internal Constructor(
                SourceMemberContainerTypeSymbol container,
                RecordDeclarationSyntax syntax,
                DiagnosticBag diagnostics)
                : base(container, WellKnownMemberNames.InstanceConstructorName)
            {
                var binder = container.GetBinder(syntax.ParameterList);

                var parameters = ParameterHelpers.MakeParameters(
                    binder, this, syntax.ParameterList, out var arglistToken,
                    allowRefOrOut: true,
                    allowThis: false,
                    addRefReadOnlyModifier: true,
                    diagnostics: diagnostics);

                if (arglistToken.Kind() == SyntaxKind.ArgListKeyword)
                {
                    // This is a parse-time error in the native compiler; it is a semantic analysis error in Roslyn.

                    // error CS1669: __arglist is not valid in this context
                    diagnostics.Add(ErrorCode.ERR_IllegalVarArgs, new SourceLocation(arglistToken));
                }

                Parameters = parameters;
                ReturnTypeWithAnnotations = TypeWithAnnotations.Create(
                    binder.GetSpecialType(SpecialType.System_Void, diagnostics, syntax));
            }

            public override MethodKind MethodKind => MethodKind.Constructor;
            public override bool ReturnsVoid => true;
            public override RefKind RefKind => RefKind.None;
            public override TypeWithAnnotations ReturnTypeWithAnnotations { get; }
            public override ImmutableArray<ParameterSymbol> Parameters { get; }
            public override bool IsOverride => false;
            internal sealed override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false) => false;
            internal override bool IsMetadataFinal => false;
            public override ImmutableArray<Location> Locations => ContainingSymbol.Locations;
            internal override bool HasSpecialName => true;

            internal override void GenerateMethodBody(TypeCompilationState compilationState, DiagnosticBag diagnostics)
            {
                //  Method body:
                //
                //  {
                //      Object..ctor();
                //      this.backingField_1 = arg1;
                //      ...
                //      this.backingField_N = argN;
                //  }
                SyntheticBoundNodeFactory F = CreateBoundNodeFactory(compilationState, diagnostics);

                int paramCount = ParameterCount;

                // List of statements
                BoundStatement[] statements = new BoundStatement[paramCount + 2];
                int statementIndex = 0;

                //  explicit base constructor call
                Debug.Assert(ContainingType.BaseTypeNoUseSiteDiagnostics.SpecialType == SpecialType.System_Object);
                BoundExpression call = MethodCompiler.GenerateBaseParameterlessConstructorInitializer(this, diagnostics);

                if (call == null)
                {
                    // This may happen if Object..ctor is not found or is inaccessible
                    return;
                }

                statements[statementIndex++] = F.ExpressionStatement(call);

                if (paramCount > 0)
                {
                    var properties = this
                        .ContainingType
                        .GetMembers()
                        .WhereAsArray(s => s.Kind == SymbolKind.Property)
                        .SelectAsArray(s => (Property)s);

                    Debug.Assert(properties.Length == paramCount);

                    // Assign fields
                    for (int index = 0; index < ParameterCount; index++)
                    {
                        // Generate 'field' = 'parameter' statement
                        statements[statementIndex++] =
                            F.Assignment(F.Field(F.This(), properties[index].BackingField), F.Parameter(Parameters[index]));
                    }
                }

                // Final return statement
                statements[statementIndex++] = F.Return();

                // Create a bound block 
                F.CloseMethod(F.Block(statements));
            }
        }

        private sealed class ToStringMethodSymbol : SynthesizedMethodBase
        {
            internal ToStringMethodSymbol(NamedTypeSymbol container)
                : base(container, WellKnownMemberNames.ObjectToString)
            {
                var stringType = container.DeclaringCompilation
                    .GetSpecialType(SpecialType.System_String);

                ReturnTypeWithAnnotations = TypeWithAnnotations.Create(
                    stringType, NullableAnnotation.NotAnnotated);
            }

            public override TypeWithAnnotations ReturnTypeWithAnnotations { get; }
            public override MethodKind MethodKind => MethodKind.Ordinary;
            public override bool ReturnsVoid => false;
            public override RefKind RefKind => RefKind.None;
            public override ImmutableArray<ParameterSymbol> Parameters => ImmutableArray<ParameterSymbol>.Empty;
            public override bool IsOverride => true;
            internal override bool IsMetadataFinal => false;
            internal override bool HasSpecialName => false;

            internal sealed override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false) => true;

            internal override void GenerateMethodBody(TypeCompilationState compilationState, DiagnosticBag diagnostics)
            {
                SyntheticBoundNodeFactory F = CreateBoundNodeFactory(compilationState, diagnostics);

                //  Method body:
                //
                //  {
                //      return String.Format(
                //          "{ <name1> = {0}", <name2> = {1}", ... <nameN> = {N-1}",
                //          this.backingFld_1, 
                //          this.backingFld_2, 
                //          ...
                //          this.backingFld_N
                //  }

                // Type expression
                var compilation = compilationState.Compilation;
                var System_Object = compilation.GetSpecialType(SpecialType.System_Object);
                var System_String = compilation.GetSpecialType(SpecialType.System_String);
                var System_String__Format_IFormatProvider = compilation.GetWellKnownTypeMember(WellKnownMember.System_String__Format_IFormatProvider) as MethodSymbol;
                var System_Object__ToString = compilation.GetSpecialTypeMember(SpecialMember.System_Object__ToString) as MethodSymbol;

                //  build arguments
                var properties = ContainingType
                    .GetMembers()
                    .WhereAsArray(s => s.Kind == SymbolKind.Property)
                    .SelectAsArray(s => (Property)s);

                int fieldCount = properties.Length;

                //  process properties
                BoundExpression[] arguments = new BoundExpression[fieldCount];
                var formatString = PooledStringBuilder.GetInstance();
                formatString.Builder.AppendFormat("{0} ", ContainingType.Name);

                for (int i = 0; i < fieldCount; i++)
                {
                    var property = properties[i];

                    // build format string
                    if (i == 0)
                    {
                        formatString.Builder.Append("{{ ");
                    }
                    else
                    {
                        formatString.Builder.Append(", ");
                    }

                    formatString.Builder.AppendFormat("{0} = ", property.Name);

                    if (Equals(property.Type, System_String))
                    {
                        formatString.Builder.AppendFormat("\"{{{0}}}\"", i);
                    }
                    else
                    {
                        formatString.Builder.AppendFormat("{{{0}}}", i);
                    }

                    // build argument
                    arguments[i] = F.Convert(
                        System_Object,
                        F.Field(F.This(), property.BackingField),
                        Conversion.ImplicitReference);
                }

                formatString.Builder.Append(" }}");

                //  add format string argument
                BoundExpression format = F.Literal(formatString.ToStringAndFree());

                //  Generate expression for return statement
                //      retExpression <= System.String.Format(args)
                var retExpression = F.StaticCall(
                    System_String,
                    System_String__Format_IFormatProvider,
                    F.Null(System_String__Format_IFormatProvider.Parameters[0].Type),
                    format,
                    F.ArrayOrEmpty(System_Object, arguments));

                F.CloseMethod(F.Block(F.Return(retExpression)));
            }
        }

        private sealed class EqualsMethodSymbol : SynthesizedMethodBase
        {
            private readonly ImmutableArray<ParameterSymbol> _parameters;

            internal EqualsMethodSymbol(NamedTypeSymbol container)
                : base(container, WellKnownMemberNames.ObjectEquals)
            {
                var System_Object = container.DeclaringCompilation.GetSpecialType(SpecialType.System_Object);
                _parameters = ImmutableArray.Create<ParameterSymbol>(
                    SynthesizedParameterSymbol.Create(this,
                        TypeWithAnnotations.Create(System_Object), 0, RefKind.None, "value"));
            }

            public override MethodKind MethodKind => MethodKind.Ordinary;
            public override bool ReturnsVoid => false;
            public override RefKind RefKind => RefKind.None;
            public override TypeWithAnnotations ReturnTypeWithAnnotations =>
                TypeWithAnnotations.Create(DeclaringCompilation.GetSpecialType(SpecialType.System_Boolean));
            public override ImmutableArray<ParameterSymbol> Parameters => _parameters;
            public override bool IsOverride => true;
            internal override bool IsMetadataFinal => false;
            internal override bool HasSpecialName => false;

            internal sealed override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false) => true;

            internal override void GenerateMethodBody(TypeCompilationState compilationState, DiagnosticBag diagnostics)
            {
                //  Method body:
                //
                //  {
                //      $anonymous$ local = value as $anonymous$;
                //      return local != null 
                //             && System.Collections.Generic.EqualityComparer<T_1>.Default.Equals(this.backingFld_1, local.backingFld_1)
                //             ...
                //             && System.Collections.Generic.EqualityComparer<T_N>.Default.Equals(this.backingFld_N, local.backingFld_N);
                //  }

                SyntheticBoundNodeFactory F = CreateBoundNodeFactory(compilationState, diagnostics);

                // Type and type expression
                var compilation = compilationState.Compilation;
                var System_Object = compilation.GetSpecialType(SpecialType.System_Object);
                var System_Boolean = compilation.GetSpecialType(SpecialType.System_Boolean);
                var System_Collections_Generic_EqualityComparer_T__Equals = compilation.GetWellKnownTypeMember(WellKnownMember.System_Collections_Generic_EqualityComparer_T__Equals) as MethodSymbol;
                var System_Collections_Generic_EqualityComparer_T__get_Default = compilation.GetWellKnownTypeMember(WellKnownMember.System_Collections_Generic_EqualityComparer_T__get_Default) as MethodSymbol;

                var properties = ContainingType
                    .GetMembers()
                    .WhereAsArray(s => s.Kind == SymbolKind.Property)
                    .SelectAsArray(s => (Property)s);

                //  local
                BoundAssignmentOperator assignmentToTemp;
                BoundLocal boundLocal = F.StoreToTemp(F.As(F.Parameter(_parameters[0]), ContainingType), out assignmentToTemp);

                //  Generate: statement <= 'local = value as $anonymous$'
                BoundStatement assignment = F.ExpressionStatement(assignmentToTemp);

                //  Generate expression for return statement
                //      retExpression <= 'local != null'
                BoundExpression retExpression = F.Binary(BinaryOperatorKind.ObjectNotEqual,
                                                         System_Boolean,
                                                         F.Convert(System_Object, boundLocal),
                                                         F.Null(System_Object));

                //  prepare symbols
                MethodSymbol equalityComparer_Equals = System_Collections_Generic_EqualityComparer_T__Equals;
                MethodSymbol equalityComparer_get_Default = System_Collections_Generic_EqualityComparer_T__get_Default;
                NamedTypeSymbol equalityComparerType = equalityComparer_Equals.ContainingType;

                // Compare fields
                for (int index = 0; index < properties.Length; index++)
                {
                    // Prepare constructed symbols
                    var property = properties[index];
                    NamedTypeSymbol constructedEqualityComparer = equalityComparerType.Construct(property.Type);
                    FieldSymbol fieldSymbol = property.BackingField;

                    // Generate 'retExpression' = 'retExpression && System.Collections.Generic.EqualityComparer<T_index>.
                    //                                                  Default.Equals(this.backingFld_index, local.backingFld_index)'
                    retExpression =
                        F.LogicalAnd(retExpression,
                            F.Call(
                                F.StaticCall(
                                    constructedEqualityComparer,
                                    equalityComparer_get_Default.AsMember(constructedEqualityComparer)),
                                equalityComparer_Equals.AsMember(constructedEqualityComparer),
                                F.Field(F.This(), fieldSymbol),
                                F.Field(boundLocal, fieldSymbol)));
                }

                // Final return statement
                BoundStatement retStatement = F.Return(retExpression);

                // Create a bound block 
                F.CloseMethod(F.Block(ImmutableArray.Create<LocalSymbol>(boundLocal.LocalSymbol), assignment, retStatement));
            }
        }

        private sealed class GetHashCodeMethodSymbol : SynthesizedMethodBase
        {
            private readonly ImmutableArray<ParameterSymbol> _parameters;

            internal GetHashCodeMethodSymbol(NamedTypeSymbol container)
                : base(container, WellKnownMemberNames.ObjectGetHashCode)
            {
            }

            public override MethodKind MethodKind => MethodKind.Ordinary;
            public override bool ReturnsVoid => false;
            public override RefKind RefKind => RefKind.None;
            public override TypeWithAnnotations ReturnTypeWithAnnotations =>
                TypeWithAnnotations.Create(DeclaringCompilation.GetSpecialType(SpecialType.System_Int32));
            public override ImmutableArray<ParameterSymbol> Parameters => ImmutableArray<ParameterSymbol>.Empty;
            public override bool IsOverride => true;
            internal override bool IsMetadataFinal => false;
            internal override bool HasSpecialName => false;

            internal sealed override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false) => true;

            internal override void GenerateMethodBody(TypeCompilationState compilationState, DiagnosticBag diagnostics)
            {
                //  Method body:
                //
                //  HASH_FACTOR = 0xa5555529;
                //  INIT_HASH = (...((0 * HASH_FACTOR) + GetFNVHashCode(backingFld_1.Name)) * HASH_FACTOR
                //                                     + GetFNVHashCode(backingFld_2.Name)) * HASH_FACTOR
                //                                     + ...
                //                                     + GetFNVHashCode(backingFld_N.Name)
                //
                //  {
                //      return (...((INITIAL_HASH * HASH_FACTOR) + EqualityComparer<T_1>.Default.GetHashCode(this.backingFld_1)) * HASH_FACTOR
                //                                               + EqualityComparer<T_2>.Default.GetHashCode(this.backingFld_2)) * HASH_FACTOR
                //                                               ...
                //                                               + EqualityComparer<T_N>.Default.GetHashCode(this.backingFld_N)
                //  }
                //
                // Where GetFNVHashCode is the FNV-1a hash code.

                const int HASH_FACTOR = -1521134295; // (int)0xa5555529

                SyntheticBoundNodeFactory F = CreateBoundNodeFactory(compilationState, diagnostics);

                // Type expression
                var compilation = compilationState.Compilation;
                var System_Int32 = compilation.GetSpecialType(SpecialType.System_Int32);
                var System_Collections_Generic_EqualityComparer_T__GetHashCode = compilation.GetWellKnownTypeMember(WellKnownMember.System_Collections_Generic_EqualityComparer_T__GetHashCode) as MethodSymbol;
                var System_Collections_Generic_EqualityComparer_T__get_Default = compilation.GetWellKnownTypeMember(WellKnownMember.System_Collections_Generic_EqualityComparer_T__get_Default) as MethodSymbol;

                var properties = ContainingType
                    .GetMembers()
                    .WhereAsArray(s => s.Kind == SymbolKind.Property)
                    .SelectAsArray(s => (Property)s);

                //  INIT_HASH
                int initHash = 0;
                foreach (var property in properties)
                {
                    initHash = unchecked(initHash * HASH_FACTOR + Hash.GetFNVHashCode(property.BackingField.Name));
                }

                //  Generate expression for return statement
                //      retExpression <= 'INITIAL_HASH'
                BoundExpression retExpression = F.Literal(initHash);

                //  prepare symbols
                MethodSymbol equalityComparer_GetHashCode = System_Collections_Generic_EqualityComparer_T__GetHashCode;
                MethodSymbol equalityComparer_get_Default = System_Collections_Generic_EqualityComparer_T__get_Default;
                NamedTypeSymbol equalityComparerType = equalityComparer_GetHashCode.ContainingType;

                //  bound HASH_FACTOR
                BoundLiteral boundHashFactor = F.Literal(HASH_FACTOR);

                // Process fields
                for (int index = 0; index < properties.Length; index++)
                {
                    // Prepare constructed symbols
                    var property = properties[index];
                    NamedTypeSymbol constructedEqualityComparer = equalityComparerType.Construct(property.Type);

                    // Generate 'retExpression' <= 'retExpression * HASH_FACTOR 
                    retExpression = F.Binary(BinaryOperatorKind.IntMultiplication, System_Int32, retExpression, boundHashFactor);

                    // Generate 'retExpression' <= 'retExpression + EqualityComparer<T_index>.Default.GetHashCode(this.backingFld_index)'
                    retExpression = F.Binary(
                        BinaryOperatorKind.IntAddition,
                        System_Int32,
                        retExpression,
                        F.Call(
                            F.StaticCall(
                                constructedEqualityComparer,
                                equalityComparer_get_Default.AsMember(constructedEqualityComparer)),
                            equalityComparer_GetHashCode.AsMember(constructedEqualityComparer),
                            F.Field(F.This(), property.BackingField)));
                }

                // Create a bound block 
                F.CloseMethod(F.Block(F.Return(retExpression)));
            }
        }

        private sealed class DeconstructMethodSymbol : SynthesizedMethodBase
        {
            internal DeconstructMethodSymbol(
                SourceMemberContainerTypeSymbol container,
                RecordDeclarationSyntax syntax,
                DiagnosticBag diagnostics)
                : base(container, WellKnownMemberNames.DeconstructMethodName)
            {
                var binder = container.GetBinder(syntax.ParameterList);
                var outToken = SyntaxFactory.Token(SyntaxKind.OutKeyword);
                var parametersWithOut = syntax.ParameterList.WithParameters(
                    SyntaxFactory.SeparatedList(
                        syntax
                            .ParameterList
                            .Parameters
                            .SelectAsArray(p => p.AddModifiers(outToken))));

                Parameters = ParameterHelpers.MakeParameters(
                    binder,
                    owner: this,
                    syntax: parametersWithOut,
                    arglistToken: out var _,
                    diagnostics: diagnostics,
                    allowRefOrOut: true,
                    allowThis: false,
                    addRefReadOnlyModifier: false);

                ReturnTypeWithAnnotations = TypeWithAnnotations.Create(
                    binder.GetSpecialType(SpecialType.System_Void, diagnostics, syntax));
            }

            public override MethodKind MethodKind => MethodKind.Ordinary;
            public override bool ReturnsVoid => true;
            public override RefKind RefKind => RefKind.None;
            public override TypeWithAnnotations ReturnTypeWithAnnotations { get; }
            public override ImmutableArray<ParameterSymbol> Parameters { get; }
            public override bool IsOverride => false;
            internal sealed override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false) => false;
            internal override bool IsMetadataFinal => false;
            public override ImmutableArray<Location> Locations => ContainingSymbol.Locations;
            internal override bool HasSpecialName => false;

            internal override void GenerateMethodBody(TypeCompilationState compilationState, DiagnosticBag diagnostics)
            {
                //  Method body:
                //
                //  {
                //      arg1 = this = this.backingField_1;
                //      ...
                //      argN = this.backingField_N;
                //  }
                SyntheticBoundNodeFactory F = CreateBoundNodeFactory(compilationState, diagnostics);

                int paramCount = ParameterCount;

                // List of statements
                BoundStatement[] statements = new BoundStatement[paramCount + 1];
                int statementIndex = 0;

                if (paramCount > 0)
                {
                    var properties = this
                        .ContainingType
                        .GetMembers()
                        .WhereAsArray(s => s.Kind == SymbolKind.Property)
                        .SelectAsArray(s => (Property)s);

                    Debug.Assert(properties.Length == paramCount);

                    // Assign fields
                    for (int index = 0; index < ParameterCount; index++)
                    {
                        // Generate 'field' = 'parameter' statement
                        statements[statementIndex++] =
                            F.Assignment(F.Parameter(Parameters[index]), F.Field(F.This(), properties[index].BackingField));
                    }
                }

                // Final return statement
                statements[statementIndex++] = F.Return();

                // Create a bound block
                F.CloseMethod(F.Block(statements));
            }
        }

        private abstract class SynthesizedMethodBase : SynthesizedInstanceMethodSymbol
        {
            public SynthesizedMethodBase(NamedTypeSymbol containingType, string name)
            {
                ContainingType = containingType;
                Name = name;
            }

            internal sealed override bool GenerateDebugInfo => false;
            public sealed override int Arity => 0;
            public sealed override Symbol ContainingSymbol => ContainingType;
            public override NamedTypeSymbol ContainingType { get; }
            public override ImmutableArray<Location> Locations => ImmutableArray<Location>.Empty;
            public sealed override Accessibility DeclaredAccessibility => Accessibility.Public;
            public sealed override bool IsStatic => false;
            public sealed override bool IsVirtual => false;
            public sealed override bool IsAsync => false;
            internal sealed override MethodImplAttributes ImplementationAttributes => default;
            internal sealed override CallingConvention CallingConvention => CallingConvention.HasThis;
            public sealed override bool IsExtensionMethod => false;
            public sealed override bool HidesBaseMethodsByName => false;
            public sealed override bool IsVararg => false;
            public sealed override FlowAnalysisAnnotations ReturnTypeFlowAnalysisAnnotations => FlowAnalysisAnnotations.None;
            public sealed override ImmutableHashSet<string> ReturnNotNullIfParameterNotNull => ImmutableHashSet<string>.Empty;
            public sealed override ImmutableArray<TypeWithAnnotations> TypeArgumentsWithAnnotations => ImmutableArray<TypeWithAnnotations>.Empty;
            public sealed override ImmutableArray<TypeParameterSymbol> TypeParameters => ImmutableArray<TypeParameterSymbol>.Empty;
            internal sealed override bool IsExplicitInterfaceImplementation => false;
            public sealed override ImmutableArray<MethodSymbol> ExplicitInterfaceImplementations => ImmutableArray<MethodSymbol>.Empty;
            internal sealed override bool IsDeclaredReadOnly => false;
            public sealed override ImmutableArray<CustomModifier> RefCustomModifiers => ImmutableArray<CustomModifier>.Empty;
            public override Symbol AssociatedSymbol => null;
            public sealed override bool IsAbstract => false;
            public sealed override bool IsSealed => false;
            public sealed override bool IsExtern => false;
            public sealed override string Name { get; }
            internal sealed override bool IsMetadataNewSlot(bool ignoreInterfaceImplementationChanges = false) => false;
            internal sealed override bool RequiresSecurityObject => false;
            public sealed override DllImportData GetDllImportData() => null;
            internal sealed override MarshalPseudoCustomAttributeData ReturnValueMarshallingInformation => null;
            internal sealed override bool HasDeclarativeSecurity => false;
            internal sealed override IEnumerable<Cci.SecurityAttribute> GetSecurityInformation() =>
                throw ExceptionUtilities.Unreachable;
            internal sealed override ImmutableArray<string> GetAppliedConditionalSymbols() => ImmutableArray<string>.Empty;
            internal override bool SynthesizesLoweredBoundBody => true;
            protected SyntheticBoundNodeFactory CreateBoundNodeFactory(TypeCompilationState compilationState, DiagnosticBag diagnostics) =>
                new SyntheticBoundNodeFactory(this, this.GetNonNullSyntaxNode(), compilationState, diagnostics)
                {
                    CurrentFunction = this
                };
            internal sealed override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) =>
                throw ExceptionUtilities.Unreachable;
        }
    }
}
