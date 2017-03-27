using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Rests;
using Terraria;
using TerrariaApi.Server;
using TerrariaServerList;
using TShockAPI;

namespace VoteAlliance
{
  [ApiVersion(2, 0)]
  public class VoteAlliance : TerrariaPlugin
  {
    private static readonly string TokenPath = Path.Combine(TShock.SavePath, "terraria-server-list-key.txt");
    private static string Token;
    private const string TokenPlaceholder = "<Paste server key here>";

    public VoteAlliance(Main game) : base(game)
    {
    }

    public override void Initialize()
    {
      ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
    }

    protected override void Dispose(bool disposing)
    {
      ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
      base.Dispose(disposing);
    }

    private static bool TryReadToken(out string token)
    {
      try
      {
        token = File.ReadAllText(TokenPath);
        return true;
      }
      catch (FileNotFoundException e)
      {
        using (var stream = File.CreateText(TokenPath))
          stream.Write(TokenPlaceholder);

        TShock.Log.ConsoleError("TerrariaServerList API key not found on {0}, disabling plugin", e.FileName);
        token = null;
        return false;
      }
    }

    private static void OnInitialize(EventArgs args)
    {
      if (!TryReadToken(out Token))
        return;

      TShock.RestApi.Register("/votealliance/query", QueryHandler);
    }

    [Route("/votealliance/query")]
    [Noun("nickname", true, "Nickname of the user to query", typeof(string))]
    private static object QueryHandler(RestRequestArgs args)
    {
      if (string.IsNullOrWhiteSpace(Token) || Token == TokenPlaceholder)
      {
        args.Context.Response.Status = HttpStatusCode.InternalServerError;
        return new RestObject("500") {{"error", "The server administrator didn't configure the plugin correctly."}};
      }

      using (var manager = new ServerManager(Token))
      {
        if (string.IsNullOrWhiteSpace(args.Parameters["nickname"]))
        {
          args.Context.Response.Status = HttpStatusCode.BadRequest;
          return new RestObject("400");
        }

        bool? result;

        try
        {
          result = manager.CheckClaimedAsync(args.Parameters["nickname"]).Result;
        }
        catch (Exception e)
        {
          TShock.Log.ConsoleError("Error while serving VoteAlliance query:\n" + e);
          args.Context.Response.Status = HttpStatusCode.InternalServerError;
          return new RestObject("500") {{"error", e.Message}};
        }

        if (result == null)
        {
          args.Context.Response.Status = HttpStatusCode.NotFound;
          return new RestObject("404")
          {
            {"description", "This means the given nickname hasn't voted today, or there's a typo in the query."}
          };
        }

        return new RestObject
        {
          {"nickname", args.Parameters["nickname"]},
          {"claimed", result.Value ? "true" : "false"}
        };
      }
    }
  }
}