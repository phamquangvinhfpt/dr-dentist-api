using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Redis;
public class RequestQueue
{
    private readonly Queue<HttpContext> _queue = new();
    private readonly RequestDelegate _next;
    public void Enqueue(HttpContext context)
    {
        lock (_queue)
        {
            _queue.Enqueue(context);
        }
    }

    public HttpContext Dequeue()
    {
        lock (_queue)
        {
            return _queue.Count > 0 ? _queue.Dequeue() : null;
        }
    }

    public int Count()
    {
        lock (_queue)
        {
            return _queue.Count;
        }
    }
}
