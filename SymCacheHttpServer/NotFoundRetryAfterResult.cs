// © Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System;
using System.Globalization;

class NotFoundRetryAfterResult : NotFoundResult
{
    public NotFoundRetryAfterResult(TimeSpan delay)
    {
        Delay = delay;
    }

    public TimeSpan Delay { get; }

    public override void ExecuteResult(ActionContext context)
    {
        base.ExecuteResult(context);
        context.HttpContext.Response.Headers[HeaderNames.RetryAfter] =
            unchecked((long)Delay.TotalSeconds).ToString(CultureInfo.InvariantCulture);
    }
}
