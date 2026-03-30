using System.Formats.Asn1;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using Twilio;
using Twilio.Jwt.AccessToken;
using Twilio.Rest.Api.V2010.Account;

[ApiController]
[Route("api/twilio/[controller]")]
public class CallController : ControllerBase
{
  private const string URL = "https://ba53-103-135-26-163.ngrok-free.app/api/twilio/Call";
  private const string RecordURL = $"{URL}/record";
  private const string StatusURL = $"{URL}/status";

  #region Get token
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
  #endregion

  #region Make Call
  private static string GetTwilioTemplateResponse(string to)
  {
    return @$"<?xml version=""1.0"" encoding=""UTF-8""?>
      <Response>
          <Say>Halo, ini panggilan dari sistem kami.</Say>

          <Dial 
            action=""{StatusURL}"" 
            callerId=""{Env.GetString("TWILIO_PHONE_NUMBER")}"" 
            record=""record-from-answer"" 
            recordingStatusCallback=""{RecordURL}""
          >
            <Number>{to}</Number>
          </Dial>
      </Response>";
  }

  [HttpPost("call")]
  public IActionResult MakeCall()
  {
    var to = Request.Form["To"].ToString();

    var response = GetTwilioTemplateResponse(to);

    return Content(response.ToString(), "text/xml");
  }
  #endregion

  #region Record
  [HttpPost("record")]
  public async Task<IActionResult> ProcessRecord()
  {
    using var httpClient = new HttpClient();

    var form = Request.Form;

    var recordingUrl = form["RecordingUrl"];
    var callSid = form["CallSid"];

    // DEBUG PURPOSE ONLY
    Console.WriteLine("=== TWILIO RECORDING CALLBACK ===");
    Console.WriteLine($"Recording URL: {recordingUrl}");
    Console.WriteLine($"Call SID: {callSid}");
    Console.WriteLine("================================");

    // Response ketika recording selesai, bisa diubah sesuai kebutuhan
    var response = @"<?xml version=""1.0"" encoding=""UTF-8""?>
      <Response>
          <Say>Terima kasih, jawaban Anda telah direkam.</Say>
      </Response>";

    return Content(response, "text/xml");
  }
  #endregion

  #region Get status
  [HttpPost("status")]
  public IActionResult CallStatus()
  {
    var form = Request.Form;

    var dialCallSid = form["DialCallSid"].ToString();
    var dialCallStatus = form["DialCallStatus"].ToString();
    var dialCallDuration = form["DialCallDuration"].ToString();

    // DEBUG PURPOSE ONLY
    Console.WriteLine("=== TWILIO STATUS CALLBACK ===");
    Console.WriteLine($"CallSid     : {dialCallSid}");
    Console.WriteLine($"CallStatus  : {dialCallStatus}");
    Console.WriteLine($"AnsweredBy  : {dialCallDuration}");
    Console.WriteLine("================================");

    return Ok();
  }
  #endregion

  #region Robot call
  [HttpGet("roboCall")]
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
  #endregion

  [HttpGet("testing")]
  public IActionResult Testing()
  {
    return Ok(new
    {
      Success = true,
      Message = "Recording received successfully!"
    });
  }

  #region ConvertToTranscript
  private async Task<byte[]> GetAudioFromRecordingUrl(HttpClient httpClient, string recordingUrl)
  {
    var accountSid = Env.GetString("TWILIO_ACCOUNT_SID") ?? throw new InvalidOperationException("TWILIO_ACCOUNT_SID environment variable is not set.");
    var authToken = Env.GetString("TWILIO_AUTH_TOKEN") ?? throw new InvalidOperationException("TWILIO_AUTH_TOKEN environment variable is not set.");

    var requestToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", requestToken);

    using var response = await httpClient.GetAsync(recordingUrl);
    if (!response.IsSuccessStatusCode)
    {
      throw new Exception($"Failed to fetch audio from recording URL. Status code: {response.StatusCode}");
    }

    return await response.Content.ReadAsByteArrayAsync();
  }

  private async Task<string> ConvertToTranscription(HttpClient httpClient, byte[] audioBytes)
  {
    var elevenLabsApiKey = Env.GetString("ELEVENLABS_API_KEY") ?? throw new InvalidOperationException("ELEVENLABS_API_KEY environment variable is not set.");

    httpClient.DefaultRequestHeaders.Add("xi-api-key", elevenLabsApiKey);

    using var form = new MultipartFormDataContent();

    var fileContent = new ByteArrayContent(audioBytes);
    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");

    form.Add(fileContent, "file", $"transcription-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid()}.wav");

    // optional param
    form.Add(new StringContent("scribe_v2"), "model_id");

    var response = await httpClient.PostAsync(
        "https://api.elevenlabs.io/v1/speech-to-text",
        form
    );

    return await response.Content.ReadAsStringAsync();
  }

  private async Task<string> ConvertToTranscriptionOpenAI(HttpClient httpClient, byte[] audioBytes)
  {
    using var form = new MultipartFormDataContent();

    // file
    using var fileStream = new MemoryStream(audioBytes);
    form.Add(new StreamContent(fileStream), "file", "sample.mp3");

    // model
    form.Add(new StringContent("gpt-4o-transcribe"), "model");


    var openAiApiKey = Env.GetString("OPENAI_API_KEY");

    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey);
    var response = await httpClient.PostAsync("https://api.openai.com/v1/audio/transcriptions", form);

    return await response.Content.ReadAsStringAsync();


    // Implementation for converting audio bytes to text transcription
    return "Transcription would be here (this is a placeholder).";
  }
  #endregion
}
