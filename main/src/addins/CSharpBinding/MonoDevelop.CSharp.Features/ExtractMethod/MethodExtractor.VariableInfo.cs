﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis;

namespace ICSharpCode.NRefactory6.CSharp.ExtractMethod
{
    public abstract partial class MethodExtractor
    {
        protected class VariableInfo
        {
            private readonly VariableSymbol _variableSymbol;
            private readonly VariableStyle _variableStyle;
            private readonly bool _useAsReturnValue;

            public VariableInfo(
                VariableSymbol variableSymbol,
                VariableStyle variableStyle,
                bool useAsReturnValue = false)
            {
                _variableSymbol = variableSymbol;
                _variableStyle = variableStyle;
                _useAsReturnValue = useAsReturnValue;
            }

            public bool UseAsReturnValue
            {
                get
                {
                    //Contract.ThrowIfFalse(!_useAsReturnValue || _variableStyle.ReturnStyle.ReturnBehavior != ReturnBehavior.None);
                    return _useAsReturnValue;
                }
            }

            public bool CanBeUsedAsReturnValue
            {
                get
                {
                    return _variableStyle.ReturnStyle.ReturnBehavior != ReturnBehavior.None;
                }
            }

            public bool UseAsParameter
            {
                get
                {
                    return (!_useAsReturnValue && _variableStyle.ParameterStyle.ParameterBehavior != ParameterBehavior.None) ||
                           (_useAsReturnValue && _variableStyle.ReturnStyle.ParameterBehavior != ParameterBehavior.None);
                }
            }

            public ParameterBehavior ParameterModifier
            {
                get
                {
                    return _useAsReturnValue ? _variableStyle.ReturnStyle.ParameterBehavior : _variableStyle.ParameterStyle.ParameterBehavior;
                }
            }

            public DeclarationBehavior GetDeclarationBehavior(CancellationToken cancellationToken)
            {
                if (_useAsReturnValue)
                {
                    return _variableStyle.ReturnStyle.DeclarationBehavior;
                }

                if (_variableSymbol.GetUseSaferDeclarationBehavior(cancellationToken))
                {
                    return _variableStyle.ParameterStyle.SaferDeclarationBehavior;
                }

                return _variableStyle.ParameterStyle.DeclarationBehavior;
            }

            public ReturnBehavior ReturnBehavior
            {
                get
                {
                    if (_useAsReturnValue)
                    {
                        return _variableStyle.ReturnStyle.ReturnBehavior;
                    }

                    return ReturnBehavior.None;
                }
            }

            public static VariableInfo CreateReturnValue(VariableInfo variable)
            {
                //Contract.ThrowIfNull(variable);
                //Contract.ThrowIfFalse(variable.CanBeUsedAsReturnValue);
                //Contract.ThrowIfFalse(variable.ParameterModifier == ParameterBehavior.Out || variable.ParameterModifier == ParameterBehavior.Ref);

                return new VariableInfo(variable._variableSymbol, variable._variableStyle, useAsReturnValue: true);
            }

            public void AddIdentifierTokenAnnotationPair(
                List<Tuple<SyntaxToken, SyntaxAnnotation>> annotations, CancellationToken cancellationToken)
            {
                _variableSymbol.AddIdentifierTokenAnnotationPair(annotations, cancellationToken);
            }

            public string Name
            {
                get { return _variableSymbol.Name; }
            }

            public bool OriginalTypeHadAnonymousTypeOrDelegate
            {
                get { return _variableSymbol.OriginalTypeHadAnonymousTypeOrDelegate; }
            }

            public ITypeSymbol GetVariableType(SemanticDocument document)
            {
                return document.SemanticModel.ResolveType(_variableSymbol.OriginalType);
            }

            public SyntaxToken GetIdentifierTokenAtDeclaration(SemanticDocument document)
            {
                return document.GetTokenWithAnnotaton(_variableSymbol.IdentifierTokenAnnotation);
            }

            public SyntaxToken GetIdentifierTokenAtDeclaration(SyntaxNode node)
            {
                return node.GetAnnotatedTokens(_variableSymbol.IdentifierTokenAnnotation).SingleOrDefault();
            }

            public static int Compare(VariableInfo left, VariableInfo right)
            {
                return VariableSymbol.Compare(left._variableSymbol, right._variableSymbol);
            }
        }
    }
}
