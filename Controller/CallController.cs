using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using Twilio;
using Twilio.Jwt.AccessToken;
using Twilio.Rest.Api.V2010.Account;

[ApiController]
[Route("api/twilio/[controller]")]
public class CallController : ControllerBase
{
  private const string URL = "https://unflippantly-fusilly-hortensia.ngrok-free.dev/api/twilio/";

  private string GetTwilioToken(string callerIdentity)
  {
    string accountSid = Env.GetString("TWILIO_ACCOUNT_SID") ?? throw new InvalidOperationException("TWILIO_ACCOUNT_SID environment variable is not set.");
    string apiKey = Env.GetString("TWILIO_API_KEY") ?? throw new InvalidOperationException("TWILIO_API_KEY environment variable is not set.");
    string apiSecret = Env.GetString("TWILIO_API_SECRET") ?? throw new InvalidOperationException("TWILIO_API_SECRET environment variable is not set.");
    string twimlAppSid = Env.GetString("TWIML_APP_SID") ?? throw new InvalidOperationException("TWIML_APP_SID environment variable is not set.");

    var voiceGrant = new VoiceGrant
    {
      OutgoingApplicationSid = twimlAppSid,
      IncomingAllow = true
    };

    var grants = new HashSet<IGrant> { { voiceGrant } };

    var token = new Token(accountSid, apiKey, apiSecret, identity: callerIdentity, grants: grants);

    return token.ToJwt();
  }

  [HttpGet("token")]
  public IActionResult Token(string callerIdentity)
  {
    try
    {
      var token = GetTwilioToken(callerIdentity);

      return Ok(new
      {
        success = true,
        tokenLength = token.Length,
        token = token
      });
    }
    catch (Exception ex)
    {
      return BadRequest(new { success = false, error = ex.Message });
    }
  }

  private static string GetTwilioTemplateResponse(string to)
  {
    return @$"<?xml version=""1.0"" encoding=""UTF-8""?>
      <Response>
          <Say>Halo, ini panggilan dari sistem kami.</Say>

          <Dial callerId=""{Env.GetString("TWILIO_PHONE_NUMBER")}"">
            <Number>{to}</Number>
          </Dial>

          <Record 
            action=""{URL}Call/process-record"" 
            maxLength=""30"" 
            playBeep=""true"" 
          />
      </Response>";
  }

  [HttpPost("call")]
  public IActionResult MakeCall()
  {
    var to = Request.Form["To"].ToString();

    var response = GetTwilioTemplateResponse(to);

    return Content(response.ToString(), "text/xml");
  }

  [HttpPost("record")]
  public IActionResult ProcessRecord()
  {
    var form = Request.Form;

    var recordingUrl = form["RecordingUrl"];
    var callSid = form["CallSid"];

    // DEBUG PURPOSE ONLY
    Console.WriteLine($"Recording URL: {recordingUrl}");
    Console.WriteLine($"Call SID: {callSid}");

    // Do something with the recording URL, e.g., save it to a database or process it further

    // Response ketika recording selesai, bisa diubah sesuai kebutuhan
    var response = @"<?xml version=""1.0"" encoding=""UTF-8""?>
      <Response>
          <Say>Terima kasih, jawaban Anda telah direkam.</Say>
      </Response>";

    return Content(response, "text/xml");
  }

  [HttpPost("status")]
  public IActionResult CallStatus()
  {
    var form = Request.Form;

    var callSid = form["CallSid"].ToString();
    var callStatus = form["CallStatus"].ToString();
    var answeredBy = form["AnsweredBy"].ToString(); // bisa kosong

    // DEBUG PURPOSE ONLY
    Console.WriteLine("=== TWILIO STATUS CALLBACK ===");
    Console.WriteLine($"CallSid     : {callSid}");
    Console.WriteLine($"CallStatus  : {callStatus}");
    Console.WriteLine($"AnsweredBy  : {answeredBy}");
    Console.WriteLine("================================");

    return Ok(); // WAJIB 200 biar Twilio anggap sukses
  }

  [HttpGet("callSomeOne")]
  public async Task<IActionResult> CallSomeone()
  {
    try
    {
      // Find your Account SID and Auth Token at twilio.com/console
      // and set the environment variables. See http://twil.io/secure
      string accountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID") ?? throw new InvalidOperationException("TWILIO_ACCOUNT_SID environment variable is not set.");
      string authToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN") ?? throw new InvalidOperationException("TWILIO_AUTH_TOKEN environment variable is not set.");

      TwilioClient.Init(accountSid, authToken);

      var call = await CallResource.CreateAsync(
          twiml: new Twilio.Types.Twiml(@$"
              <Response>
                  <Say>Halo, ini panggilan dari sistem kami.</Say>
          
                  <Dial>
                    <Number>+62811842223</Number>
                  </Dial>

                  <Record 
                      action=""{URL}Call/process-record"" 
                      maxLength=""30"" 
                      playBeep=""true"" />
              </Response>
            "),
          to: new Twilio.Types.PhoneNumber("+6281387306360"),
          from: new Twilio.Types.PhoneNumber("+15188004785"),
          statusCallback: new Uri($"{URL}Call/status")
      );

      return Ok(new
      {
        Success = true,
        CallSID = call.Sid,
        Message = "Call initiated successfully!"
      });
    }
    catch (Exception ex)
    {
      return BadRequest(new
      {
        Success = false,
        Message = $"Error initiating call: {ex.Message}"
      });
    }
  }

  [HttpPost("voice")]
  public IActionResult Voice()
  {
    var twiml = @$"<?xml version=""1.0"" encoding=""UTF-8""?>
      <Response>
          <Say>Halo, ini panggilan dari sistem kami.</Say>

          <Record 
            action=""{URL}Call/process-record"" 
            maxLength=""30"" 
            playBeep=""true"" 
          />
      </Response>";

    return Content(twiml, "text/xml");
  }

  [HttpGet("testing")]
  public IActionResult Testing()
  {
    return Ok(new
    {
      Success = true,
      Message = "Recording received successfully!"
    });
  }
}
