using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Google
{
    public partial class GoogleAlertAPI
    {
        private string email = string.Empty;
        private string password = string.Empty;
        private string auth_id = string.Empty;
        private string auth_token = string.Empty;
        private Hashtable AlertTable = new Hashtable();
        private HttpClient client;
        private HttpClientHandler httpClientHandler;
        private CookieContainer cookieContainer = new CookieContainer();

        private string SCRIPT_JSON_TAG = "window.STATE = ";
        private string GOOGLE_LOGIN_URL = "https://accounts.google.com/ServiceLoginAuth";

        public GoogleAlertAPI(string email, string password)
        {   
            this.email = email;
            this.password = password;
            httpClientHandler = new HttpClientHandler() {
                                        CookieContainer = cookieContainer,
                                        UseCookies = true,
                                        UseDefaultCredentials = false
                                    };
            client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml");
            //client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Charset", "ISO-8859-1");

            TryLogin();
        }

        private void TryLogin()
        {
            Task<string> loginTask = getUrlString(GOOGLE_LOGIN_URL);
            Task.WaitAll(loginTask);
            string body = loginTask.Result;
            List<KeyValuePair<string, string>> postData = getLoginFormParams(body, email, password);

            loginTask = postUrlString(GOOGLE_LOGIN_URL, postData);
            Task.WaitAll(loginTask);
            body = loginTask.Result;

            if (hasElement(body, "//span[@id[starts-with(.,'errormsg')]]"))
            {
                throw new Exception("Login error.");
            }

            Task<string> alertTask = postUrlString("https://www.google.com/alerts?hl=en&", null);
            Task.WaitAll(alertTask);
            body = alertTask.Result;
            List<string> script_list = getElements(body,"//script");
            foreach (string script in script_list)
            {
                if (script.StartsWith(SCRIPT_JSON_TAG))
                {
                    string json_str = script.Substring(script.IndexOf(SCRIPT_JSON_TAG) + SCRIPT_JSON_TAG.Length, script.Length - SCRIPT_JSON_TAG.Length -1);
                    JArray json = JArray.Parse(json_str);
                    if (json[1] == null)
                    {
                        break;
                    }                    
                    JToken alert_item_token = json[1];
                    if (!IsNullOrEmpty(alert_item_token))
                    {
                        foreach (var alert_item in alert_item_token[1])
                        {
                            AlertTable[alert_item[1].ToString()] = alert_item[2][6][0][11].ToString();
                        }
                    }
                    auth_id = json[3].ToString();
                    auth_token = json[2][6][0][14].ToString();
                }
            }
            if (string.IsNullOrEmpty(auth_id) || string.IsNullOrEmpty(auth_token))
            {
                throw new Exception("Login error.");
            }
        }

        public List<Alert> getAlerts()
        {
            List<Alert> alert_list = new List<Alert>();
            if (!string.IsNullOrEmpty(auth_id))
            {
                Task<string> alertTask = postUrlString("https://www.google.com/alerts?hl=en&", null);
                Task.WaitAll(alertTask);
                string body = alertTask.Result;

                List<string> script_list = getElements(body, "//script");
                foreach (string script in script_list)
                {
                    if (script.StartsWith(SCRIPT_JSON_TAG))
                    {
                        string json_str = script.Substring(script.IndexOf(SCRIPT_JSON_TAG) + SCRIPT_JSON_TAG.Length, script.Length - SCRIPT_JSON_TAG.Length - 1);
                        JArray json = JArray.Parse(json_str);
                        JToken alert_item_token = json[1];
                        if (IsNullOrEmpty(alert_item_token) || json[1] == null)
                        {
                            break;
                        }
                        if (IsNullOrEmpty(alert_item_token) || json[1][1] == null || json[1][1].Count() <= 0)
                        {
                            break;
                        }
                        foreach (var alert_item in json[1][1])
                        {
                            Alert alert = new Alert();
                            alert.feedUrl = "https://www.google.com/alerts/feeds/" + alert_item[3]+ "/" +alert_item[2][6][0][11];
                            alert.id = alert_item[1].ToString();
                            alert.query = alert_item[2][3][1].ToString();
                            Enum.TryParse<HowMany>(alert_item[2][5].ToString(), out alert.howMany);
                            Enum.TryParse<HowOften>(alert_item[2][6][0][4].ToString(), out alert.howOften);
                            alert.language = GetEnumValueFromDescription<Language>(alert_item[2][3][3][1].ToString());
                            alert.region = GetEnumValueFromDescription<Region>(alert_item[2][3][3][2].ToString());
                            
                            if (alert_item[2][4] != null)
                            {
                                for (int i = 0; i < alert_item[2][4].Count(); i++)
                                {   
                                    alert.sources.Add(GetEnumValueFromDescription<Sources>(alert_item[2][4][i].ToString()));
                                }
                            }
                            if (alert_item[2][6][0][1].ToString() == "1")
                            {
                                alert.deliveryTo = DeliveryTo.Email;
                            }
                            else
                            {
                                alert.deliveryTo = DeliveryTo.Feed;
                            }
                            alert_list.Add(alert);
                        }
                        auth_id = json[3].ToString();
                        break;
                    }
                }
            }
            return alert_list;
        }

        public string createAlert(Alert alert)
        {  
            string id = string.Empty;
            if (!string.IsNullOrEmpty(auth_id) && alert != null)
            {
                List<KeyValuePair<string,string>> postData = new List<KeyValuePair<string,string>>();
                postData.Add(new KeyValuePair<string,string>("params",createPostParameter(alert,false)));
                Task<string> createTask = postUrlString("https://www.google.com/alerts/create?hl=en&", postData);
                Task.WaitAll(createTask);
                string body = createTask.Result;
                JArray json = JArray.Parse(body);
                if (IsNullOrEmpty(json) || json.Count()<5)
                {
                    throw new Exception("Create alert error!");
                }
                else
                {
                    id = json[4][0][1].ToString();
                    AlertTable[id] = json[4][0][3][6][0][11].ToString();
                    if (alert.deliveryTo == DeliveryTo.Feed)
                    {
                        
                        var link_text = json[4][0][2].ToString();
                        Match match = Regex.Match(link_text, @"/alerts/feeds/[\w^/]+/[\w^/]+",RegexOptions.Compiled);
                        if (match.Success)
                        {   
                            alert.feedUrl = "https://www.google.com" + match.Value;
                        }
                    }
                    
                }
            }
            return id;
        }

        public string modifyAlert(Alert alert)
        {
            string id = string.Empty;
            if (!string.IsNullOrEmpty(auth_id) && (alert != null) && (!string.IsNullOrEmpty(alert.id)))
            {
                List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                postData.Add(new KeyValuePair<string, string>("params", createPostParameter(alert, true)));
                Task<string> createTask = postUrlString("https://www.google.com/alerts/modify?hl=en&", postData);
                Task.WaitAll(createTask);
                string body = createTask.Result;
                JArray json = JArray.Parse(body);
                id = json[4][0][1].ToString();
                AlertTable[id] = json[4][0][3][6][0][11].ToString();
            }
            return id;
        }

        public bool deleteAlert(string alert_id)
        {
            if (!string.IsNullOrEmpty(auth_id) && !string.IsNullOrEmpty(alert_id))
            {
                List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                postData.Add(new KeyValuePair<string, string>("params", "[null,\"" + alert_id + "\"]"));
                Task<string> deleteTask = postUrlString("https://www.google.com/alerts/delete?hl=en&", postData);
                Task.WaitAll(deleteTask);
                return true;
            }
            return false;
        }

        private string createPostParameter(Alert alert, bool isEdit)
        {   
            string domainExt = "com";
            string feedKey = "1";
            string _email = email;
            HowOften _howOften = alert.howOften;
            if (alert.deliveryTo == DeliveryTo.Feed)
            {
                feedKey = "2";
                _howOften = HowOften.AsItHappens;
                _email = string.Empty;
            }
            if (!string.IsNullOrEmpty(_email))
            {
                domainExt = _email.Substring(_email.LastIndexOf(".") + 1);
            }
            string _sources = string.Empty;
            foreach (var source in alert.sources)
            {
                if (string.IsNullOrEmpty(_sources))
                {
                    _sources += GetDescriptionFromEnumValue(source);
                }
                else
                {
                    _sources += "," + GetDescriptionFromEnumValue(source);
                }
            }
            _sources = "[" + _sources +"]";
            if (_sources == "[]")
            {
                _sources = "null";
            }
            string howOften = string.Empty;
            switch(_howOften){
                case HowOften.AsItHappens:
                    howOften = "[]," + (int)_howOften;
                    break;
                case HowOften.AtMostOnceADay:
                    howOften = "[null,null,3]," + (int)_howOften;
                    break;
                case HowOften.AtMostOnceAWeek:
                    howOften = "[null,null,3,3]," + (int)_howOften;
                    break;
            }
            string update = string.Empty;
            if (isEdit)
            {
                update = "\"" + alert.id + "\",";
            }
            string flagLanguage = "1";
            Language _language = alert.language;
            if (alert.language == Language.AnyLanguage)
            {
                _language = Language.English;
                flagLanguage = "0";
            }
            string flagRegion = "1";
            Region _region = alert.region;
            if (alert.region == Region.AnyRegion)
            {
                _region = Region.UnitedStates;
                flagRegion = "0";
            }
            string query = alert.query;

            return "[null," + update + "[null,null,null,[null,\"" + query + "\",\"" + domainExt + "\",[null,\"" + GetDescriptionFromEnumValue(_language) + "\",\"" + GetDescriptionFromEnumValue(_region) + "\"],null,null,null," + flagRegion + "," + flagLanguage + "]," + _sources + "," + (int)alert.howMany + ",[[null," + feedKey + ",\"" + _email + "\"," + howOften + ",\"en-US\"," + (isEdit ? "1" : "null") + ",null,null,null,null,\"" + (isEdit ? (string)this.AlertTable[alert.id] : "0") + "\",null,null,\"" + this.auth_token + "\"]]]]";
        }

        private async Task<string> getUrlString(string url)
        {
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            Task<string> body = response.Content.ReadAsStringAsync();
            return await Task.Run(() => body.Result);
        }

        private async Task<string> postUrlString(string url, List<KeyValuePair<string, string>> postData)
        {
            if (postData == null)
            {
                postData = new List<KeyValuePair<string, string>>();
            }
            HttpContent postContent = new FormUrlEncodedContent(postData);
            if ((postData != null) && (postData.Count == 1) && postData[0].Key == "params")
            {
                url = url + "x=" + this.auth_id;
            }
            HttpResponseMessage response = await client.PostAsync(url, postContent);
            response.EnsureSuccessStatusCode();
            Task<string> body = response.Content.ReadAsStringAsync();
            return await Task.Run(() => body.Result);
        }

        private bool hasElement(string body, string xpath)
        {
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(body);
            HtmlAgilityPack.HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(xpath);
            return nodes != null && nodes.Count > 0;
        }

        private List<string> getElements(string body, string xpath)
        {
            List<string> list = new List<string>();
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(body);
            HtmlAgilityPack.HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(xpath);
            foreach (var node in nodes)
            {
                list.Add(node.InnerHtml);
            }
            return list;
        }

        private List<KeyValuePair<string, string>> getLoginFormParams(string body, string username, string password)
        {
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(body);
            HtmlAgilityPack.HtmlNode loginform = doc.GetElementbyId("gaia_loginform");
            HtmlAgilityPack.HtmlNodeCollection inputElements = loginform.SelectNodes("//input");
            List<KeyValuePair<string, string>> paramList = new List<KeyValuePair<string, string>>();
            bool hasPassword = false;
            foreach (HtmlAgilityPack.HtmlNode input in inputElements)
            {
                string name = input.GetAttributeValue("name", string.Empty);
                string value = input.GetAttributeValue("value", string.Empty);
                if (name == "Email")
                {
                    value = username;
                }
                else if (name == "Passwd")
                {
                    hasPassword = true;
                    value = password;
                }
                paramList.Add(new KeyValuePair<string, string>(name, value));
            }
            if (!hasPassword)
            {
                paramList.Add(new KeyValuePair<string, string>("Passwd", password));
            }
            return paramList;
        }


        private static string GetDescriptionFromEnumValue(Enum value)
        {
            DescriptionAttribute attribute = value.GetType()
                .GetField(value.ToString())
                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .SingleOrDefault() as DescriptionAttribute;
            return attribute == null ? value.ToString() : attribute.Description;
        }

        private static T GetEnumValueFromDescription<T>(string description)
        {
            var type = typeof(T);
            if (!type.IsEnum)
                throw new ArgumentException();
            FieldInfo[] fields = type.GetFields();
            var field = fields
                            .SelectMany(f => f.GetCustomAttributes(
                                typeof(DescriptionAttribute), false), (
                                    f, a) => new { Field = f, Att = a })
                            .Where(a => ((DescriptionAttribute)a.Att)
                                .Description == description).SingleOrDefault();
            return field == null ? default(T) : (T)field.Field.GetRawConstantValue();
        }

        public static bool IsNullOrEmpty(JToken token)
        {
            return (token == null) ||
                   (token.Type == JTokenType.Array && !token.HasValues) ||
                   (token.Type == JTokenType.Object && !token.HasValues) ||
                   (token.Type == JTokenType.String && token.ToString() == String.Empty) ||
                   (token.Type == JTokenType.Null);
        }
    }
}
