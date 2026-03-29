// MIT License
//
// Copyright (c) 2026 BruceMellows
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BruceMellows.MVVM.ViewModel.Proxy;

public sealed class InvalidTargetTypeException : Exception
{
	public InvalidTargetTypeException(string? paramName)
		: base(paramName)
	{
	}

	public static void ThrowIfInvalid(Type targetType, [CallerArgumentExpression(nameof(targetType))] string? paramName = null)
	{
		if (!targetType.IsInterface)
		{
			Throw(paramName);
		}

		if (targetType != typeof(INotifyPropertyChanged) && targetType != typeof(INotifyPropertyChanging))
		{
			var propertyMemberNames = targetType
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.SelectMany(p => new[] { p.GetMethod, p.SetMethod }.Where(m => m is not null).Select(m => m!.Name))
				.ToHashSet();
			var members = targetType.GetMembers(BindingFlags.Public | BindingFlags.Instance);
			bool allMembersAreProperties = members.All(member => member.MemberType == MemberTypes.Property || propertyMemberNames.Contains(member.Name));

			if (!allMembersAreProperties)
			{
				Throw(paramName);
			}
		}
	}

	[DoesNotReturn]
	private static void Throw(string? paramName)
	{
		throw new InvalidTargetTypeException(paramName);
	}
}