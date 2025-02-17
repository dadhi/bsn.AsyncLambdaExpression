using System;
using System.Linq.Expressions;

using bsn.AsyncLambdaExpression.Collections;

namespace bsn.AsyncLambdaExpression.Expressions {
	internal partial class ContinuationBuilder {
		protected override Expression VisitLoop(LoopExpression node) {
			if (!node.ContainsAsyncCode(true)) {
				return node;
			}
			var continueState = this.GetLabelState(node.ContinueLabel ?? Expression.Label());
			this.currentState.SetContinuation(continueState);
			this.currentState = continueState;
			var body = this.Visit(node.Body);
			this.currentState.AddExpression(body);
			this.currentState.SetContinuation(continueState);
			this.currentState = node.BreakLabel == null
					? new AsyncState(-1, node.Type, ImmutableStack<TryInfo>.Empty)
					: this.GetLabelState(node.BreakLabel);
			return this.currentState.ResultExpression;
		}
	}
}
