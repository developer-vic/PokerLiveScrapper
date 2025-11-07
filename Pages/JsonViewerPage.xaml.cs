using System.Text;

namespace PokerLiveScrapper.Pages;

public partial class JsonViewerPage : ContentPage
{
    private string _jsonData = "";

    public JsonViewerPage(string jsonData)
    {
        InitializeComponent();
        _jsonData = jsonData;
        LoadJson(jsonData);
    }

    private void LoadJson(string jsonData)
    {
        if (string.IsNullOrEmpty(jsonData))
        {
            jsonData = "{}";
        }

        // Parse JSON to ensure it's valid and format it properly
        string formattedJson;
        try
        {
            // Parse and reformat JSON
            var parsed = System.Text.Json.JsonSerializer.Deserialize<object>(jsonData);
            formattedJson = System.Text.Json.JsonSerializer.Serialize(parsed, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
        catch
        {
            formattedJson = jsonData;
        }

        // Use base64 encoding for safe JSON injection
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(formattedJson);
        var base64Json = Convert.ToBase64String(jsonBytes);
        
        // Create HTML with syntax highlighting CSS
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'/>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: 'Courier New', Consolas, monospace;
            font-size: 14px;
            line-height: 1.6;
            background-color: #ffffff;
            color: #333;
            padding: 15px;
            overflow-x: auto;
        }}
        pre {{
            background-color: #f8f8f8;
            border: 1px solid #e0e0e0;
            border-radius: 4px;
            padding: 15px;
            overflow-x: auto;
            white-space: pre-wrap;
            word-wrap: break-word;
        }}
        .json-key {{
            color: #881391;
            font-weight: bold;
        }}
        .json-string {{
            color: #1a1aa6;
        }}
        .json-number {{
            color: #0e7b0e;
        }}
        .json-boolean {{
            color: #0e7b0e;
            font-weight: bold;
        }}
        .json-null {{
            color: #808080;
            font-style: italic;
        }}
    </style>
</head>
<body>
    <pre id='json-display'></pre>
    <script>
        function syntaxHighlight(json) {{
            if (typeof json != 'string') {{
                json = JSON.stringify(json, null, 2);
            }}
            json = json.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
            return json.replace(/(\""[^""]*""\s*:|\b(true|false|null)\b|-?\d+(?:\.\d*)?(?:[eE][+\-]?\d+)?)/g, function (match) {{
                var cls = 'json-number';
                if (/""[^""]*""\s*:/.test(match)) {{
                    cls = 'json-key';
                }} else if (/^"".*""$/.test(match)) {{
                    cls = 'json-string';
                }} else if (/true|false/.test(match)) {{
                    cls = 'json-boolean';
                }} else if (/null/.test(match)) {{
                    cls = 'json-null';
                }}
                return '<span class=""' + cls + '"">' + match + '</span>';
            }});
        }}
        
        try {{
            var base64Json = '{base64Json}';
            var jsonString = atob(base64Json);
            var jsonData = JSON.parse(jsonString);
            document.getElementById('json-display').innerHTML = syntaxHighlight(JSON.stringify(jsonData, null, 2));
        }} catch (e) {{
            document.getElementById('json-display').innerHTML = '<span style=""color: red;"">Error parsing JSON: ' + e.message + '</span><br><br><pre>' + (typeof atob !== 'undefined' ? atob('{base64Json}') : 'Base64 decode not available') + '</pre>';
        }}
    </script>
</body>
</html>";

        var htmlSource = new HtmlWebViewSource
        {
            Html = html
        };

        JsonWebView.Source = htmlSource;
    }

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_jsonData))
        {
            await Clipboard.Default.SetTextAsync(_jsonData);
            await DisplayAlert("Copied", "JSON data copied to clipboard!", "OK");
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}

