using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AutoReplyBot;

// Global variables that will be injected into C# script
public record Global(string UserName, int UserNo);

public class Matcher
{
    private readonly List<Rule> _rules;
    private readonly int _takes;
    private readonly ILogger<Matcher> _logger;

    public Matcher(List<Rule> rules, int takes, ILogger<Matcher> logger)
    {
        _rules = rules;
        _takes = takes;
        _logger = logger;
    }

    public class Action
    {
        public Action(string replyContent, string? emotionType, double? triggerChance)
        {
            ReplyContent = replyContent;
            EmotionType = emotionType;
            TriggerChance = triggerChance;
        }

        public string ReplyContent { get; set; }
        public string? EmotionType { get; set; }
        public double? TriggerChance { get; set; }
    }


    public Task<Action[]> Match(Comment? comment, string content, int userNo, string userName)
    {
        // throw away @username when matching
        try
        {
            content = Regex.Replace(content, @"<band:refer[^>]*>[^<]*</band:refer>", "");
        }
        catch (RegexMatchTimeoutException e)
        {
            _logger.LogError(e, null);
        }

        if (content.Contains("I am a bot")) return Task.FromResult(Array.Empty<Action>());
        var actions = _rules
            .Where(r => (r.Keywords.Contains("*") ||
                         (r.IgnoreCase != false &&
                          r.Keywords.Any(k => content.Contains(k, StringComparison.OrdinalIgnoreCase))) ||
                         (r.IgnoreCase == false && r.Keywords.Any(content.Contains))) &&
                        (r.TargetAuthors.Contains(userName) || r.TargetAuthors.Contains("*")))
            .Where(r => (r.Type == null || (r.Type != null &&
                                            ((r.Type == "post" && comment!.CommentId == 0) ||
                                             (r.Type == "comment" && comment!.CommentId != 0)))))
            .Take(_takes)
            .Select(async r =>
            {
                var reply = r.Replies[Random.Shared.Next(r.Replies.Count)];
                return reply.ReplyType switch
                {
                    ReplyType.PlainText => new Action(reply.Data.Trim(), r.EmotionType, r.TriggerChance),
                    ReplyType.CSharpScript => new Action(await reply.Script!(new Global(userName, userNo)),
                        r.EmotionType,
                        r.TriggerChance),
                    _ => throw new InvalidOperationException()
                };
            });
        return Task.WhenAll(actions);
    }
}