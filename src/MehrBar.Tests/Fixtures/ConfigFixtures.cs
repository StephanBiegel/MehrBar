namespace MehrBar.Tests.Fixtures;

/// <summary>Hand-written config.json texts for the serializer tests (CONF-02).</summary>
internal static class ConfigFixtures
{
    /// <summary>A file from a "newer build": a group item, unknown fields on every level, "type" not first.</summary>
    public const string Future = """
        {
          "version": 1,
          "rootExtra": { "a": 1 },
          "general": { "startWithWindows": false, "skinFolder": "Skins", "generalExtra": "x" },
          "bars": [
            {
              "id": "b1",
              "name": "Oben",
              "edge": "top",
              "align": "center",
              "edgeOffset": -5,
              "barExtra": [1, 2, 3],
              "label": { "show": true, "fontFamily": "Tahoma", "fontSize": 13, "labelExtra": true },
              "items": [
                { "name": "Firefox", "path": "C:\\Program Files\\Mozilla Firefox\\firefox.exe", "type": "app", "itemExtra": 7 },
                { "type": "group", "name": "Tools", "collapsed": true, "items": [ { "type": "app", "path": "C:\\a.exe" } ] },
                { "type": "folder", "name": "Downloads", "path": "%USERPROFILE%\\Downloads" },
                { "type": "separator" },
                { "type": "url", "name": "Wetter", "url": "https://www.dwd.de" }
              ]
            }
          ]
        }
        """;

    /// <summary>Smallest valid file.</summary>
    public const string Minimal = """
        { "version": 1, "bars": [ { "id": "b1", "items": [] } ] }
        """;

    /// <summary>Unknown enum value.</summary>
    public const string BadEnum = """
        { "version": 1, "bars": [ { "id": "b1", "edge": "diagonal", "items": [] } ] }
        """;

    /// <summary>One app item with a number as path; the other item is fine.</summary>
    public const string BadItemValue = """
        { "version": 1, "bars": [ { "id": "b1", "items": [
          { "type": "app", "name": "Kaputt", "path": 42 },
          { "type": "app", "name": "Gut", "path": "C:\\ok.exe" }
        ] } ] }
        """;

    /// <summary>A root value of the wrong kind.</summary>
    public const string BadRoot = """
        { "version": 1, "bars": 5 }
        """;

    /// <summary>Comments and trailing commas, as a hand-edited file might have them.</summary>
    public const string WithComments = """
        {
          // hand-edited
          "version": 1,
          "bars": [ { "id": "b1", "items": [], }, ],
        }
        """;
}
