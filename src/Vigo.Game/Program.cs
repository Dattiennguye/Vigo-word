using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Vigo.Core;
using Vigo.Core.CityBuilder;
using Vigo.Core.Military;

namespace Vigo.Game
{
    /// <summary>
    /// Máy chủ cục bộ cho bản chơi trên trình duyệt: giao diện HTML5 gọi API JSON, mọi logic nằm ở Vigo.Core (GameSession).
    /// Chạy: dotnet run --project Vigo.Game  → mở http://localhost:5077
    /// </summary>
    public static class Program
    {
        private static readonly object Gate = new object();
        private static GameSession Game;
        private static string SaveDir = Path.Combine(AppContext.BaseDirectory, "saves");

        public static int Main(string[] args)
        {
            string root = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (!Directory.Exists(root)) root = Path.Combine(Directory.GetCurrentDirectory(), "Vigo.Game", "wwwroot");
            if (!Directory.Exists(root)) root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = root });
            string url = Environment.GetEnvironmentVariable("VIGO_URL") ?? "http://localhost:5077";
            builder.WebHost.UseUrls(url);
            builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
            var app = builder.Build();
            app.UseDefaultFiles(); app.UseStaticFiles();
            var api = new Vigo.Api.GameApi(SaveDir);
            app.Map("/api/{**rest}", async (HttpContext ctx) =>
            {
                string body = ""; if (ctx.Request.Method == "POST") { using (var r = new StreamReader(ctx.Request.Body)) body = await r.ReadToEndAsync(); }
                var (status, json) = api.Handle(ctx.Request.Method, ctx.Request.Path.Value + ctx.Request.QueryString.Value, body);
                ctx.Response.StatusCode = status; ctx.Response.ContentType = "application/json; charset=utf-8"; await ctx.Response.WriteAsync(json);
            });
            Console.WriteLine("VIGO đang chạy tại " + url);
            app.Run();
            return 0;
        }
    }
}
