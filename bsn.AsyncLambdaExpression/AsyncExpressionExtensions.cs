using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using bsn.AsyncLambdaExpression.Expressions;

using JetBrains.Annotations;

namespace bsn.AsyncLambdaExpression {
	public static class AsyncExpressionExtensions {
		private const string MessageNotConvertedToAsyncStateMachine = "The lambda expression was not converted to an async state machine";

		private static readonly MethodInfo meth_AwaitResult = typeof(AsyncExpressionExtensions).GetMethod(nameof(AwaitResult), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
		private static readonly MethodInfo meth_AwaitVoid = typeof(AsyncExpressionExtensions).GetMethod(nameof(AwaitVoid), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

		public static bool IsAwaitMethod(MethodInfo method) {
			if (!method.IsGenericMethod) {
				return false;
			}
			var methodDefinition = method.GetGenericMethodDefinition();
			return methodDefinition == meth_AwaitResult || methodDefinition == meth_AwaitVoid;
		}

		// ReSharper disable once UnusedParameter.Local
		private static TResult AwaitResult<TAwaitable, TResult>(TAwaitable awaitable) {
			throw new NotSupportedException(MessageNotConvertedToAsyncStateMachine);
		}

		// ReSharper disable once UnusedParameter.Local
		private static void AwaitVoid<TAwaitable>(TAwaitable awaitable) {
			throw new NotSupportedException(MessageNotConvertedToAsyncStateMachine);
		}

		public static Expression Await([NotNull] this Expression expression, bool? continueOnCapturedContext = null) {
			if (expression == null) {
				throw new ArgumentNullException(nameof(expression));
			}
			if (continueOnCapturedContext.HasValue) {
				var meth_ConfigureAwait = expression.Type.GetAwaitableGetConfigureAwaitMethod();
				if (meth_ConfigureAwait == null) {
					throw new ArgumentException($"The type {expression.Type.FullName} of the expression does not have a ConfigureAwait(bool) method", nameof(expression));
				}
				expression = Expression.Call(expression, meth_ConfigureAwait, Expression.Constant(continueOnCapturedContext.Value));
			}
			var methGetAwaiter = expression.Type.GetAwaitableGetAwaiterMethod();
			if (methGetAwaiter == null) {
				throw new ArgumentException($"The type {expression.Type.FullName} of the expression is not awaitable", nameof(expression));
			}
			var methGetResult = methGetAwaiter.ReturnType.GetAwaiterGetResultMethod();
			return Expression.Call(methGetResult.ReturnType == typeof(void)
					? meth_AwaitVoid.MakeGenericMethod(expression.Type)
					: meth_AwaitResult.MakeGenericMethod(expression.Type, methGetResult.ReturnType), expression);
		}

		public static Expression<T> Async<T>(this LambdaExpression expression, bool debug = false) {
			return Expression.Lambda<T>(new AsyncStateMachineBuilder(expression, typeof(T).GetDelegateInvokeMethod().ReturnType).CreateStateMachineBody(debug), expression.Parameters);
		}

		public static LambdaExpression Async(this LambdaExpression expression, Type delegateType, bool debug = false) {
			return Expression.Lambda(delegateType, new AsyncStateMachineBuilder(expression, delegateType.GetDelegateInvokeMethod().ReturnType).CreateStateMachineBody(debug), expression.Parameters);
		}

		internal static Expression RescopeVariables(this Expression expression, IEnumerable<ParameterExpression> unmanagedVariablesAndParameters) {
			var finder = new VariableScopeFinder(unmanagedVariablesAndParameters);
			finder.Visit(expression);
			var setter = new VariableScopeSetter(finder.GetBlockVariables(), finder.IsIgnored, finder.IsToRemove);
			return setter.Visit(expression);
		}

		internal static Expression Optimize(this Expression expression) {
			var optimizer = new Optimizer();
			return optimizer.Visit(expression);
		}

		internal static bool ContainsAsyncCode(this Expression expression, bool labelAndGotoAreAsync) {
			var awaitCallChecker = new AsyncChecker(labelAndGotoAreAsync);
			awaitCallChecker.Visit(expression);
			return awaitCallChecker.HasAsyncCode;
		}

		internal static bool IsSafeCode(this Expression expression) {
			var safeCodeChecker = new SafeCodeChecker();
			safeCodeChecker.Visit(expression);
			return !safeCodeChecker.ContainsUnsafeCode;
		}
	}
}
