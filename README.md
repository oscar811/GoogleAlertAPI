# GoogleAlertAPI

### Usage
Create Alert
```csharp
Google.GoogleAlertAPI ga = new Google.GoogleAlertAPI("xxx@gmail.com","XXXXX");
Google.GoogleAlertAPI.Alert alert = new Google.GoogleAlertAPI.Alert();
alert.query = "GoogleAlertAPI";
alert.region = Google.GoogleAlertAPI.Region.Taiwan;
alert.language = Google.GoogleAlertAPI.Language.ChineseTraditional;
alert.deliveryTo = Google.GoogleAlertAPI.DeliveryTo.Feed;
alert.howMany = Google.GoogleAlertAPI.HowMany.AllResults;
alert.sources.Add(Google.GoogleAlertAPI.Sources.Blogs);
alert.sources.Add(Google.GoogleAlertAPI.Sources.Web);
ga.createAlert(alert);
```

List all Alert
```csharp
Google.GoogleAlertAPI ga = new Google.GoogleAlertAPI("xxx@gmail.com","XXXXX");
List<Google.GoogleAlertAPI.Alert> list = ga.getAlerts();
foreach(var alert in list){
  // alert object
}
```

Delete Alert
```csharp
Google.GoogleAlertAPI ga = new Google.GoogleAlertAPI("xxx@gmail.com","XXXXX");
ga.deleteAlert(alert.id);
```

Modify Alert
```csharp
Google.GoogleAlertAPI ga = new Google.GoogleAlertAPI("xxx@gmail.com","XXXXX");
ga.modifyAlert(alert);
```


