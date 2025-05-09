﻿using System.Linq.Expressions;
using AnyOfTypes;

namespace System.Linq.Dynamic.Core.Parser;

internal interface IKeywordsHelper
{
    bool IsItOrRootOrParent(AnyOf<string, Expression, Type> value);

    bool TryGetValue(string text, out AnyOf<string, Expression, Type> value);
}