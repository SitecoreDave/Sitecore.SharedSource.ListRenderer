/* Copyright (c) [2016] [David Walker] - MIT License - see License.txt */
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Xml.Linq;
using Sitecore.Configuration;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Links;
using Sitecore.Resources.Media;
using Sitecore.Web;
using Sitecore.Web.UI;

namespace Sitecore.SharedSource.ListRenderer.Views.Renderings
{
    public class ListRenderer : WebControl
    {
        private static string ConfigKey => typeof (ListRenderer).Module.Name.Replace(".dll","");

        private Item _currentItem;

        private readonly string _fieldsPropertyKey = GetSetting("FieldsPropertyKey", "Fields");
        private readonly string _templatesExcludedPropertyKey = GetSetting("TemplatesExcludedPropertyKey", "Templates Excluded");
        private readonly string _templatesIncludedPropertyKey = GetSetting("TemplatesIncludedPropertyKey", "Templates Included");
        private readonly string _listFormatPropertyKey = GetSetting("_ListFormatPropertyKey", "List Format");
        private readonly string _itemFormatPropertyKey = GetSetting("ItemFormatPropertyKey", "Item Format");
        private readonly string _separatorPropertyKey = GetSetting("SeparatorPropertyKey", "Separator");
        private readonly string _otherTextPropertyKey = GetSetting("OtherTextPropertyKey", "Other Text");
        private readonly string _breadcrumbDataSourceKey = GetSetting("BreadcrumbDataSourceKey", "{5F7C869E-C630-4027-B0FF-407275D7F1B8}");
        private readonly string _dataFormatTokenKey = GetSetting("DataFormatTokenKey", "{Data}");
        private readonly string _itemFormatDefault = GetSetting("ItemFormatDefault", "<li id=\"{1}\">{0}</li>");
        private readonly string _dataSourceTemplateId = GetSetting("DataSourceTemplateId", "{F997B3CF-6CDE-404D-9921-8A3A8B70C9F6}");
        private List<string> _fieldNames = new List<string>();
        private List<string> _templatesExcluded = new List<string>();
        private List<string> _templatesIncluded = new List<string>();

        private NameValueCollection _parameters;
        private string _itemFormat;
        private string _listFormat;
        private string _separator;

        protected override void DoRender(HtmlTextWriter output)
        {
            try
            {
                Diagnostics.Log.Debug(ConfigKey + ".DoRender", this);

                _parameters = WebUtil.ParseUrlParameters(Parameters);
                _fieldNames = SplitToList(_fieldsPropertyKey);
                _templatesExcluded = SplitToList(_templatesExcludedPropertyKey);
                _templatesIncluded = SplitToList(_templatesIncludedPropertyKey);

                output.Write(!DataSource.StartsWith("http") ? GetSitecoreContent() : GetWebContent());

                Diagnostics.Log.Info(ConfigKey + ".DoRender: " + UniqueID + "-" + Parameters, this);
            }
            catch (Exception exception)
            {
                Diagnostics.Log.Error(exception.Message, exception, this);
            }
        }

        private static string GetSetting(string name, string defaultValue)
        {
            Diagnostics.Log.Debug(ConfigKey + ".GetSetting(" + name + "," + defaultValue + "): started", typeof(ListRenderer));

            var results = Settings.GetSetting(ConfigKey + "." + name, defaultValue);

            Diagnostics.Log.Info(ConfigKey + ".GetSetting(" + name + "," + defaultValue + "): " + results, typeof(ListRenderer));
            
            return results;
            
        }

        public Item CurrentItem
        {
            get
            {
                if (_currentItem != null) return _currentItem;

                if (string.IsNullOrEmpty(DataSource) || DataSource == _breadcrumbDataSourceKey)
                {
                    if (Sitecore.Context.Item != null)
                    {
                        _currentItem = Sitecore.Context.Item;
                    }

                    Diagnostics.Log.Info(ConfigKey + ".CurrentItem: " + (_currentItem?.ID.ToString() ?? "null"), this);

                    return _currentItem;
                }

                _currentItem = Sitecore.Context.Database.GetItem(DataSource);

                Diagnostics.Log.Info(ConfigKey + ".CurrentItem: " + (_currentItem?.ID.ToString() ?? "null"), this);

                return _currentItem;
            }

            set
            {
                _currentItem = value;
            }
        }

        public string ControlId => _parameters["id"];

        public string ItemFormat
        {
            get
            {
                if (!string.IsNullOrEmpty(_itemFormat)) return _itemFormat;

                _itemFormat = string.IsNullOrEmpty(_parameters[_itemFormatPropertyKey]) ? _itemFormatDefault : _parameters[_itemFormatPropertyKey];
                _itemFormat = CommonTags(_itemFormat);
                return _itemFormat;
            }
        }

        public string SeparatorFormat
        {
            get
            {
                if (!string.IsNullOrEmpty(_separator)) return _separator;

                _separator = string.IsNullOrEmpty(_parameters[_separatorPropertyKey]) ? "" : _parameters[_separatorPropertyKey];
                _separator = CommonTags(_separator);
                return _itemFormat;
            }
        }

        public string ListFormat
        {
            get
            {
                if (!string.IsNullOrEmpty(_listFormat)) return _listFormat;

                _listFormat = !string.IsNullOrEmpty(_parameters[_listFormatPropertyKey]) ? _parameters[_listFormatPropertyKey] : "";
                _listFormat = CommonTags(_listFormat);
                if (_listFormat.Contains(" id=\"\"")) _listFormat = _listFormat.Replace(" id=\"\"", "");
                if (_listFormat.Contains(" class=\"\"")) _listFormat = _listFormat.Replace(" class=\"\"", "");
                if (string.IsNullOrEmpty(_listFormat) || !_listFormat.Contains(_dataFormatTokenKey))
                {
                    _listFormat += _dataFormatTokenKey;
                }
                return _listFormat;
            }
            set { _listFormat = value; }
        }

        public bool OtherText
        {
            get
            {
                if (string.IsNullOrEmpty(_parameters[_otherTextPropertyKey])) return false;

                var otherTextValue = _parameters[_otherTextPropertyKey];

                return otherTextValue == "1";
            }
        }

        private List<string> SplitToList(string parameterName)
        {
            return string.IsNullOrEmpty(_parameters[parameterName]) ? new List<string>() : _parameters[parameterName].Split(',').ToList();
        }


        private string CommonTags(string formatString)
        {
            if (formatString.Contains("{Id}")) formatString = formatString.Replace("{Id}", ControlId);
            if (formatString.Contains("{CssClass}")) formatString = formatString.Replace("{CssClass}", CssClass);
            return formatString;
        }

        private List<Item> SubItems
        {
            get
            {
                return CurrentItem?.Children.Where(c => (_templatesExcluded.Any() && !_templatesExcluded.Contains(c.TemplateName) || _templatesIncluded.Any() && _templatesIncluded.Contains(c.TemplateName)) && !string.IsNullOrEmpty(LinkManager.GetItemUrl(c)) && !string.IsNullOrEmpty(c.Fields["Title"]?.ToString())).ToList();
            }
        }

        public List<Item> Breadcrumb
        {
            get
            {
                var itemsInPath = new List<Item>();
                var currentItem = Sitecore.Context.Item.Parent;
                if (currentItem.Name == "Content") return itemsInPath;
                while (currentItem.Key != "content" && (!_templatesExcluded.Any() || !_templatesExcluded.Contains(currentItem.TemplateName) && (!_templatesIncluded.Any() || _templatesIncluded.Contains(currentItem.TemplateName))))
                {
                    itemsInPath.Add(currentItem);
                    currentItem = currentItem.Parent;
                }
                itemsInPath.Reverse();
                return itemsInPath;
            }
        }
        
        private string GetSitecoreContent()
        {
            if (CurrentItem != null && CurrentItem.TemplateID.ToString() == _dataSourceTemplateId)
            {
                return GetDataSourceContent();
            }

            var results = new StringBuilder();

            try
            {
                var itemFormat = ItemFormat;

                if (!_fieldNames.Any())
                {
                    _fieldNames.Add("DisplayName");
                }

                var breadcrumb = !string.IsNullOrEmpty(DataSource) && DataSource == _breadcrumbDataSourceKey;

                var items = !breadcrumb ? SubItems : Breadcrumb;

                foreach (var item in items)
                {
                    try
                    {
                        var thisItemFormat = itemFormat;
                        if (thisItemFormat.Contains("{Link}"))
                        {
                            thisItemFormat = thisItemFormat.Replace("{Link}", LinkManager.GetItemUrl(item));
                        }
                        results.Append(string.Format(thisItemFormat,
                            _fieldNames.Select(field => GetSitecoreFieldValue(item, field)).Cast<object>().ToArray()));

                        if (!string.IsNullOrEmpty(SeparatorFormat) && (item.ID != items.Last().ID || breadcrumb))
                        {
                            results.Append(string.Format(SeparatorFormat,
                            _fieldNames.Select(field => GetSitecoreFieldValue(item, field)).Cast<object>().ToArray()));
                        }
                    }
                    catch (Exception exception)
                    {
                        Diagnostics.Log.Error(exception.Message, exception, this);
                    }
                }

                if (OtherText)
                {
                    var otherTextString = string.Format(itemFormat, "Other", "Other");
                    otherTextString = otherTextString.Replace("</li>",
                        "<br/><input type=\"text\" id=\"" + ControlId + "-other-text\"/></li>");
                    results.Append(otherTextString);
                }

                if (breadcrumb)
                {
                    results.Append(GetSitecoreFieldValue(CurrentItem, "Title"));
                }

                return !string.IsNullOrEmpty(results.ToString()) ? ListFormat.Replace(_dataFormatTokenKey, results.ToString()) : string.Empty;
            }
            catch (Exception exception)
            {
                Diagnostics.Log.Error(exception.Message, exception, this);
            }

            return results.ToString();
        }

        private string GetDataSourceContent()
        {
            if (string.IsNullOrEmpty(CurrentItem?.Fields["Connection"]?.ToString()) || string.IsNullOrEmpty(CurrentItem.Fields["Query"]?.ToString())) return null;

            var connectionStringItem = Sitecore.Context.Database.GetItem(((LinkField) CurrentItem?.Fields["Connection"]).Value);

            if (string.IsNullOrEmpty(connectionStringItem?.Fields["ConnectionString"]?.ToString())) return null;

            var connectionString = ((LinkField)connectionStringItem.Fields["ConnectionString"]).Value;

            var results = new StringBuilder();

            try
            {   
                if (!_fieldNames.Any())
                {
                    _fieldNames.Add("*");
                }
                var commandText = CurrentItem?.Fields["Query"].ToString();
                
                commandText = commandText.Replace("{Fields}", string.Join(",", _fieldNames));

                //options?
                commandText = commandText.Replace("TOP 50", "TOP 15");

                var connection = new SqlConnection(connectionString);

                var command = connection.CreateCommand();
                command.CommandText = commandText;
                command.Connection.Open();
                var reader = command.ExecuteReader(CommandBehavior.CloseConnection);
                
                var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
                var thisItemFormat = string.Empty;
                if (columns.Any())
                {
                    var cellItemFormat = ItemFormat;

                    
                    for (var index = 0; index < columns.Count; index++)
                    {
                        thisItemFormat = thisItemFormat + string.Format(cellItemFormat, "{" + index + "}");
                    }
                    thisItemFormat = "<tr>" + thisItemFormat + "</tr>";
                }

                if (reader.HasRows)
                {
                    var values = new object[reader.FieldCount];
                    while (reader.Read())
                    {
                        try
                        {
                            reader.GetValues(values);
                            results.Append(string.Format(thisItemFormat, values));
                            
                            if (!string.IsNullOrEmpty(SeparatorFormat))
                            {
                                results.Append(string.Format(SeparatorFormat, values));
                            }
                        }
                        catch (Exception ex)
                        {
                            Diagnostics.Log.Error(ex.Message, ex, this);
                        }
                    }
                }

                var listFormatDefault =
                    string.IsNullOrEmpty(ListFormat) || ListFormat == _dataFormatTokenKey;
                if (listFormatDefault || ListFormat.Contains("{Fields}"))
                {
                    if (listFormatDefault)
                    {
                        ListFormat = "<table>";
                    }

                    if (ListFormat.Contains("{Fields}"))
                    {
                        var fields = columns.Aggregate("", (current, column) => current + "<th style=\"vertical-align: top;\">" + column + "</th>");// "<tr>";
                        ListFormat = ListFormat.Replace("{Fields}", "<tr>" + fields + "</tr>");
                    }

                    if (listFormatDefault)
                    {
                        ListFormat = ListFormat + _dataFormatTokenKey + "</table>";
                    }
                }
                return !string.IsNullOrEmpty(results.ToString()) ? ListFormat.Replace(_dataFormatTokenKey, results.ToString()) : string.Empty;
            }
            catch (Exception exception)
            {
                Diagnostics.Log.Error(exception.Message, exception, this);
            }

            return results.ToString();
        }

        private static string GetSitecoreFieldValue(Item item, string field)
        {
            var results = string.Empty;

            try
            {
                if (field == "DisplayName") return item.DisplayName; //field = "__Display";

                results = item[field];

                var scfield = item.Fields[field];

                if (scfield == null) return results;

                if (scfield.Type == "LinkField")
                {
                    var linkField = (LinkField)scfield;
                    results = linkField.IsInternal
                        ? LinkManager.GetItemUrl(linkField.TargetItem)
                        : linkField.Url;
                }

                if (scfield.Type != "Image") return results;

                var mediaItem = ((ImageField)scfield).MediaItem;
                if (mediaItem != null) results = MediaManager.GetMediaUrl(mediaItem);

            }
            catch (Exception ex)
            {
                Diagnostics.Log.Error(ex.Message, ex, typeof(ListRenderer));
            }

            Diagnostics.Log.Info(ConfigKey + ".GetSitecoreFieldValue(.," + field + "):" + results, typeof(ListRenderer));

            return results;
        }

        private string GetWebContent()
        {
            var results = new StringBuilder();

            try
            {
                var itemFormat = ItemFormat;

                if (!_fieldNames.Any())
                {
                    _fieldNames.Add("title");
                    _fieldNames.Add("link");
                }

                var xdocument = XDocument.Load(DataSource);

                var items = (from item in xdocument.Descendants("item") select item).ToList();

                if (!items.Any()) return string.Empty;

                foreach (var item in items)
                {
                    var thisItemFormat = itemFormat;

                    results.Append(string.Format(thisItemFormat,
                        _fieldNames.Select(field => GetElement(item, field)).Cast<object>().ToArray()));
                }
            }
            catch (Exception ex)
            {
                Diagnostics.Log.Error(ex.Message, ex, this);
            }

            Diagnostics.Log.Info(ConfigKey + ".GetWebContent(" + DataSource + "):" + results, typeof(ListRenderer));

            return results.ToString();
        }

        private static string GetElement(XContainer item, string name)
        {
            var results = string.Empty;

            try
            {
                var element = item.Element(name);
                if (element == null) return string.Empty;
                if (name != "enclosure")
                {
                    return element.Value;
                }
                var attribute = element.Attribute("url");
                return attribute?.Value ?? string.Empty;
            }
            catch (Exception exception)
            {
                Diagnostics.Log.Error(exception.Message, exception, typeof(ListRenderer));
            }

            return results;
        }
    }
}