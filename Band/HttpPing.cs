﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Band;

public class HttpPing
{
    private readonly ILogger<HttpPing> _logger;
    private readonly HttpClient _http;

    public HttpPing(ILogger<HttpPing> logger, IWebProxy? proxy = null)
    {
        _logger = logger;
        _http = new HttpClient(new HttpClientHandler { Proxy = proxy });
    }

    public async Task<long> Ping(string url)
    {
        _logger.LogDebug("Starting pinging {Url}", url);
        var stopwatch = new Stopwatch();

        #region Warmup

        {
            stopwatch.Restart();
            await _http.GetAsync(url);
            var elapsed = stopwatch.ElapsedMilliseconds;
            _logger.LogDebug("Warmup pinging {Url} : {Ping}ms", url, elapsed.ToString());
        }

        #endregion


        var totalTime = 0L;
        for (var i = 0; i < 5; i++)
        {
            stopwatch.Restart();
            await _http.GetAsync(url);
            var elapsed = stopwatch.ElapsedMilliseconds;
            _logger.LogDebug("Pinging {Url} : {Ping}ms", url, stopwatch.ElapsedMilliseconds.ToString());
            totalTime += elapsed;
        }

        var average = totalTime / 5;
        _logger.LogInformation("Pinging {Url} : Average {Ping}ms", url, average.ToString());
        return average;
    }

    public async Task<string> GetFastest(params string[] urls)
    {
        var results = urls
            .AsParallel()
            .Select(url =>
            {
                var ping = Ping(url).Result;
                _logger.LogInformation("Average ping of {Url} is: {Ping}ms", url, ping.ToString());
                return new { url, ping };
            });
        var fastest = results.MinBy(pair => pair.ping)!.url;
        _logger.LogInformation("The fastest url is {Fastest}", fastest);
        return fastest;
    }
}