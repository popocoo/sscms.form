﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datory;
using SSCMS.Configuration;
using SSCMS.Enums;
using SSCMS.Form.Abstractions;
using SSCMS.Form.Models;
using SSCMS.Form.Utils;
using SSCMS.Form.Utils.Atom.Atom.AdditionalElements;
using SSCMS.Form.Utils.Atom.Atom.AdditionalElements.DublinCore;
using SSCMS.Form.Utils.Atom.Atom.Core;
using SSCMS.Models;
using SSCMS.Repositories;
using SSCMS.Services;
using SSCMS.Utils;

namespace SSCMS.Form.Core
{
    public class FormManager : IFormManager
    {
        private const string PluginId = "sscms.form";
        public const string PermissionsForms = "form_forms";
        public const string PermissionsTemplates = "form_templates";

        private readonly ICacheManager _cacheManager;
        private readonly IPathManager _pathManager;
        private readonly IPluginManager _pluginManager;
        private readonly IFormRepository _formRepository;
        private readonly ITableStyleRepository _tableStyleRepository;
        private readonly IDataRepository _dataRepository;

        public FormManager(ICacheManager cacheManager, IPathManager pathManager, IPluginManager pluginManager, IFormRepository formRepository, ITableStyleRepository tableStyleRepository, IDataRepository dataRepository)
        {
            _cacheManager = cacheManager;
            _pathManager = pathManager;
            _pluginManager = pluginManager;
            _formRepository = formRepository;
            _tableStyleRepository = tableStyleRepository;
            _dataRepository = dataRepository;
        }

        public const string DefaultListAttributeNames = "Name,Mobile,Email,Content";

        public List<ContentColumn> GetColumns(List<string> listAttributeNames, List<TableStyle> styles, bool isReply)
        {
            var columns = new List<ContentColumn>
            {
                new ContentColumn
                {
                    AttributeName = nameof(DataInfo.Id),
                    DisplayName = "Id",
                    IsList = ListUtils.ContainsIgnoreCase(listAttributeNames, nameof(DataInfo.Id))
                },
                new ContentColumn
                {
                    AttributeName = nameof(DataInfo.Guid),
                    DisplayName = "编号",
                    IsList = ListUtils.ContainsIgnoreCase(listAttributeNames, nameof(DataInfo.Guid))
                }
            };

            foreach (var style in styles)
            {
                if (string.IsNullOrEmpty(style.DisplayName) || style.InputType == InputType.TextEditor) continue;

                var column = new ContentColumn
                {
                    AttributeName = style.AttributeName,
                    DisplayName = style.DisplayName,
                    InputType = style.InputType,
                    IsList = ListUtils.ContainsIgnoreCase(listAttributeNames, style.AttributeName)
                };

                columns.Add(column);
            }

            columns.AddRange(new List<ContentColumn>
            {
                new ContentColumn
                {
                    AttributeName = nameof(DataInfo.CreatedDate),
                    DisplayName = "添加时间",
                    IsList = ListUtils.ContainsIgnoreCase(listAttributeNames, nameof(DataInfo.CreatedDate))
                },
                new ContentColumn
                {
                    AttributeName = nameof(DataInfo.LastModifiedDate),
                    DisplayName = "更新时间",
                    IsList = ListUtils.ContainsIgnoreCase(listAttributeNames, nameof(DataInfo.LastModifiedDate))
                }
            });

            if (isReply)
            {
                columns.AddRange(new List<ContentColumn>
                {
                    new ContentColumn
                    {
                        AttributeName = nameof(DataInfo.ReplyDate),
                        DisplayName = "回复时间",
                        IsList = ListUtils.ContainsIgnoreCase(listAttributeNames, nameof(DataInfo.ReplyDate))
                    },
                    new ContentColumn
                    {
                        AttributeName = nameof(DataInfo.ReplyContent),
                        DisplayName = "回复内容",
                        IsList = ListUtils.ContainsIgnoreCase(listAttributeNames, nameof(DataInfo.ReplyContent))
                    }
                });
            }

            return columns;
        }

        public async Task<DataInfo> GetDataInfoAsync(int dataId, int formId, List<TableStyle> styles)
        {
            DataInfo dataInfo;
            if (dataId > 0)
            {
                dataInfo = await _dataRepository.GetDataInfoAsync(dataId);
            }
            else
            {
                dataInfo = new DataInfo
                {
                    FormId = formId
                };

                foreach (var style in styles)
                {
                    if (style.InputType == InputType.Text || style.InputType == InputType.TextArea || style.InputType == InputType.TextEditor || style.InputType == InputType.Hidden)
                    {
                        if (string.IsNullOrEmpty(style.DefaultValue)) continue;

                        dataInfo.Set(style.AttributeName, style.DefaultValue);
                    }
                    else if (style.InputType == InputType.Number)
                    {
                        if (string.IsNullOrEmpty(style.DefaultValue)) continue;

                        dataInfo.Set(style.AttributeName, TranslateUtils.ToInt(style.DefaultValue));
                    }
                    else if (style.InputType == InputType.CheckBox || style.InputType == InputType.SelectMultiple)
                    {
                        var value = new List<string>();

                        if (style.Items != null)
                        {
                            foreach (var item in style.Items)
                            {
                                if (item.Selected)
                                {
                                    value.Add(item.Value);
                                }
                            }
                        }

                        dataInfo.Set(style.AttributeName, value);
                    }
                    else if (style.InputType == InputType.Radio || style.InputType == InputType.SelectOne)
                    {
                        if (style.Items != null)
                        {
                            foreach (var item in style.Items)
                            {
                                if (item.Selected)
                                {
                                    dataInfo.Set(style.AttributeName, item.Value);
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty(style.DefaultValue))
                        {
                            dataInfo.Set(style.AttributeName, style.DefaultValue);
                        }
                    }
                }
            }

            return dataInfo;
        }

        public async Task CreateDefaultStylesAsync(FormInfo formInfo)
        {
            var relatedIdentities = GetRelatedIdentities(formInfo.Id);

            await _tableStyleRepository.InsertAsync(relatedIdentities, new TableStyle
            {
                TableName = FormUtils.TableNameData,
                RelatedIdentity = relatedIdentities[0],
                AttributeName = "Name",
                DisplayName = "姓名",
                HelpText = "请输入您的姓名",
                InputType = InputType.Text,
                Rules = new List<InputStyleRule>
                {
                    new InputStyleRule
                    {
                        Type = ValidateType.Required,
                        Message = ValidateType.Required.GetDisplayName()
                    }
                }
            });

            await _tableStyleRepository.InsertAsync(relatedIdentities, new TableStyle
            {
                TableName = FormUtils.TableNameData,
                RelatedIdentity = relatedIdentities[0],
                AttributeName = "Mobile",
                DisplayName = "手机",
                HelpText = "请输入您的手机号码",
                InputType = InputType.Text,
                Rules = new List<InputStyleRule>
                {
                    new InputStyleRule
                    {
                        Type = ValidateType.Mobile,
                        Message = ValidateType.Mobile.GetDisplayName()
                    }
                }
            });

            await _tableStyleRepository.InsertAsync(relatedIdentities, new TableStyle
            {
                TableName = FormUtils.TableNameData,
                RelatedIdentity = relatedIdentities[0],
                AttributeName = "Email",
                DisplayName = "邮箱",
                HelpText = "请输入您的电子邮箱",
                InputType = InputType.Text,
                Rules = new List<InputStyleRule>
                {
                    new InputStyleRule
                    {
                        Type = ValidateType.Email,
                        Message = ValidateType.Email.GetDisplayName()
                    }
                }
            });

            await _tableStyleRepository.InsertAsync(relatedIdentities, new TableStyle
            {
                TableName = FormUtils.TableNameData,
                RelatedIdentity = relatedIdentities[0],
                AttributeName = "Content",
                DisplayName = "留言",
                HelpText = "请输入您的留言",
                InputType = InputType.TextArea,
                Rules = new List<InputStyleRule>
                {
                    new InputStyleRule
                    {
                        Type = ValidateType.Required,
                        Message = ValidateType.Required.GetDisplayName()
                    }
                }
            });
        }

        public async Task DeleteAsync(int siteId, int formId)
        {
            if (formId <= 0) return;

            var formInfo = await _formRepository.GetFormInfoAsync(siteId, formId);
            var relatedIdentities = GetRelatedIdentities(formInfo.Id);

            await _tableStyleRepository.DeleteAllAsync(FormUtils.TableNameData, relatedIdentities);
            await _dataRepository.DeleteByFormIdAsync(formId);
            await _formRepository.DeleteAsync(siteId, formId);
        }

        private const string VersionFileName = "version.txt";

        private static bool IsHistoric(string directoryPath)
        {
            if (!FileUtils.IsFileExists(PathUtils.Combine(directoryPath, VersionFileName))) return true;

            FileUtils.DeleteFileIfExists(PathUtils.Combine(directoryPath, VersionFileName));

            return false;
        }

        public async Task ImportFormAsync(int siteId, string directoryPath, bool overwrite)
        {
            if (!Directory.Exists(directoryPath)) return;
            var isHistoric = IsHistoric(directoryPath);

            var filePaths = Directory.GetFiles(directoryPath);

            foreach (var filePath in filePaths)
            {
                var feed = AtomFeed.Load(new FileStream(filePath, FileMode.Open));

                var formInfo = new FormInfo();

                foreach (var tableColumn in _formRepository.TableColumns)
                {
                    var value = GetValue(feed.AdditionalElements, tableColumn);
                    formInfo.Set(tableColumn.AttributeName, value);
                }

                formInfo.SiteId = siteId;

                if (isHistoric)
                {
                    formInfo.Title = GetDcElementContent(feed.AdditionalElements, "InputName");
                }

                var srcFormInfo = await _formRepository.GetFormInfoByTitleAsync(siteId, formInfo.Title);
                if (srcFormInfo != null)
                {
                    if (overwrite)
                    {
                        await DeleteAsync(siteId, srcFormInfo.Id);
                    }
                    else
                    {
                        formInfo.Title = await _formRepository.GetImportTitleAsync(siteId, formInfo.Title);
                    }
                }

                formInfo.Id = await _formRepository.InsertAsync(formInfo);

                var directoryName = GetDcElementContent(feed.AdditionalElements, "Id");
                if (isHistoric)
                {
                    directoryName = GetDcElementContent(feed.AdditionalElements, "InputID");
                }
                var titleAttributeNameDict = new NameValueCollection();
                if (!string.IsNullOrEmpty(directoryName))
                {
                    var fieldDirectoryPath = PathUtils.Combine(directoryPath, directoryName);
                    titleAttributeNameDict = await ImportFieldsAsync(siteId, formInfo.Id, fieldDirectoryPath, isHistoric);
                }

                var entryList = new List<AtomEntry>();
                foreach (AtomEntry entry in feed.Entries)
                {
                    entryList.Add(entry);
                }

                entryList.Reverse();

                foreach (var entry in entryList)
                {
                    var dataInfo = new DataInfo();

                    foreach (var tableColumn in _dataRepository.TableColumns)
                    {
                        var value = GetValue(entry.AdditionalElements, tableColumn);
                        dataInfo.Set(tableColumn.AttributeName, value);
                    }

                    var attributes = GetDcElementNameValueCollection(entry.AdditionalElements);
                    foreach (string entryName in attributes.Keys)
                    {
                        dataInfo.Set(entryName, attributes[entryName]);
                    }

                    if (isHistoric)
                    {
                        foreach (var title in titleAttributeNameDict.AllKeys)
                        {
                            dataInfo.Set(title, dataInfo.Get(titleAttributeNameDict[title]));
                        }

                        dataInfo.ReplyContent = GetDcElementContent(entry.AdditionalElements, "Reply");
                        if (!string.IsNullOrEmpty(dataInfo.ReplyContent))
                        {
                            dataInfo.IsReplied = true;
                        }
                        dataInfo.CreatedDate = FormUtils.ToDateTime(GetDcElementContent(entry.AdditionalElements, "adddate"));
                    }

                    await _dataRepository.InsertAsync(formInfo, dataInfo);
                }
            }
        }

        private async Task<NameValueCollection> ImportFieldsAsync(int siteId, int formId, string styleDirectoryPath, bool isHistoric)
        {
            var titleAttributeNameDict = new NameValueCollection();

            if (!Directory.Exists(styleDirectoryPath)) return titleAttributeNameDict;

            var formInfo = await _formRepository.GetFormInfoAsync(siteId, formId);
            var relatedIdentities = GetRelatedIdentities(formInfo.Id);
            await _pathManager.ImportStylesAsync(FormUtils.TableNameData, relatedIdentities, styleDirectoryPath);

            //var filePaths = Directory.GetFiles(styleDirectoryPath);
            //foreach (var filePath in filePaths)
            //{
            //    var feed = AtomFeed.Load(new FileStream(filePath, FileMode.Open));

            //    var attributeName = GetDcElementContent(feed.AdditionalElements, "AttributeName");
            //    var title = GetDcElementContent(feed.AdditionalElements, "DisplayName");
            //    if (isHistoric)
            //    {
            //        title = GetDcElementContent(feed.AdditionalElements, "DisplayName");

            //        titleAttributeNameDict[title] = attributeName;
            //    }
            //    var fieldType = GetDcElementContent(feed.AdditionalElements, nameof(TableStyle.InputType));
            //    if (isHistoric)
            //    {
            //        fieldType = GetDcElementContent(feed.AdditionalElements, "InputType");
            //    }
            //    var taxis = FormUtils.ToIntWithNegative(GetDcElementContent(feed.AdditionalElements, "Taxis"), 0);

            //    var style = new TableStyle
            //    {
            //        TableName = tableName,
            //        RelatedIdentity = relatedIdentities[0],
            //        Taxis = taxis,
            //        Title = title,
            //        InputType = TranslateUtils.ToEnum(fieldType, InputType.Text)
            //    };

            //    var fieldItems = new List<FieldItemInfo>();
            //    foreach (AtomEntry entry in feed.Entries)
            //    {
            //        var itemValue = GetDcElementContent(entry.AdditionalElements, "ItemValue");
            //        var isSelected = FormUtils.ToBool(GetDcElementContent(entry.AdditionalElements, "IsSelected"), false);

            //        fieldItems.Add(new FieldItemInfo
            //        {
            //            FormId = formId,
            //            FieldId = 0,
            //            Value = itemValue,
            //            IsSelected = isSelected
            //        });
            //    }

            //    if (fieldItems.Count > 0)
            //    {
            //        style.Items = fieldItems;
            //    }

            //    if (await _fieldRepository.IsTitleExistsAsync(formId, title))
            //    {
            //        await _fieldRepository.DeleteAsync(formId, title);
            //    }
            //    await _fieldRepository.InsertAsync(siteId, style);
            //}

            return titleAttributeNameDict;
        }

        public async Task ExportFormAsync(int siteId, string directoryPath, int formId)
        {
            var formInfo = await _formRepository.GetFormInfoAsync(siteId, formId);
            var filePath = PathUtils.Combine(directoryPath, formInfo.Id + ".xml");

            var feed = GetEmptyFeed();

            foreach (var tableColumn in _formRepository.TableColumns)
            {
                SetValue(feed.AdditionalElements, tableColumn, formInfo);
            }

            //var styleDirectoryPath = PathUtils.Combine(directoryPath, formInfo.Id.ToString());

            var relatedIdentities = GetRelatedIdentities(formInfo.Id);

            await _pathManager.ExportStylesAsync(siteId, FormUtils.TableNameData, relatedIdentities);
            //await ExportFieldsAsync(formInfo.Id, styleDirectoryPath);

            var dataInfoList = await _dataRepository.GetAllDataInfoListAsync(formInfo);
            foreach (var dataInfo in dataInfoList)
            {
                var entry = GetAtomEntry(dataInfo);
                feed.Entries.Add(entry);
            }
            feed.Save(filePath);

            var plugin = _pluginManager.GetPlugin(PluginId);

            await FileUtils.WriteTextAsync(PathUtils.Combine(directoryPath, VersionFileName), plugin.Version);
        }


        private const string Prefix = "SiteServer_";

        private string ToXmlContent(string inputString)
        {
            var contentBuilder = new StringBuilder(inputString);
            contentBuilder.Replace("<![CDATA[", string.Empty);
            contentBuilder.Replace("]]>", string.Empty);
            contentBuilder.Insert(0, "<![CDATA[");
            contentBuilder.Append("]]>");
            return contentBuilder.ToString();
        }

        private void AddDcElement(ScopedElementCollection collection, string name, string content)
        {
            if (!string.IsNullOrEmpty(content))
            {
                collection.Add(new DcElement(Prefix + name, ToXmlContent(content)));
            }
        }

        private string GetDcElementContent(ScopedElementCollection additionalElements, string name, string defaultContent = "")
        {
            var localName = Prefix + name;
            var element = additionalElements.FindScopedElementByLocalName(localName);
            return element != null ? element.Content : defaultContent;
        }

        private NameValueCollection GetDcElementNameValueCollection(ScopedElementCollection additionalElements)
        {
            return additionalElements.GetNameValueCollection(Prefix);
        }

        private AtomFeed GetEmptyFeed()
        {
            var feed = new AtomFeed
            {
                Title = new AtomContentConstruct("title", "siteserver channel"),
                Author = new AtomPersonConstruct("author",
                    "siteserver", new Uri("https://sscms.com")),
                Modified = new AtomDateConstruct("modified", DateTime.Now,
                    DateTimeOffset.UtcNow.Offset)
            };

            return feed;
        }

        private AtomEntry GetEmptyEntry()
        {
            var entry = new AtomEntry
            {
                Id = new Uri("https://sscms.com/"),
                Title = new AtomContentConstruct("title", "title"),
                Modified = new AtomDateConstruct("modified", DateTime.Now,
                    DateTimeOffset.UtcNow.Offset),
                Issued = new AtomDateConstruct("issued", DateTime.Now,
                    DateTimeOffset.UtcNow.Offset)
            };

            return entry;
        }

        private string Encrypt(string inputString)
        {
            return TranslateUtils.EncryptStringBySecretKey(inputString, "TgQQk42O");
        }

        private string Decrypt(string inputString)
        {
            return TranslateUtils.DecryptStringBySecretKey(inputString, "TgQQk42O");
        }

        private AtomEntry GetAtomEntry(Entity entity)
        {
            var entry = GetEmptyEntry();

            foreach (var keyValuePair in entity.ToDictionary())
            {
                if (keyValuePair.Value != null)
                {
                    AddDcElement(entry.AdditionalElements, keyValuePair.Key, keyValuePair.Value.ToString());
                }
            }

            return entry;
        }

        private object GetValue(ScopedElementCollection additionalElements, TableColumn tableColumn)
        {
            if (tableColumn.DataType == DataType.Boolean)
            {
                return TranslateUtils.ToBool(GetDcElementContent(additionalElements, tableColumn.AttributeName), false);
            }
            if (tableColumn.DataType == DataType.DateTime)
            {
                return FormUtils.ToDateTime(GetDcElementContent(additionalElements, tableColumn.AttributeName));
            }
            if (tableColumn.DataType == DataType.Decimal)
            {
                return FormUtils.ToDecimalWithNegative(GetDcElementContent(additionalElements, tableColumn.AttributeName), 0);
            }
            if (tableColumn.DataType == DataType.Integer)
            {
                return FormUtils.ToIntWithNegative(GetDcElementContent(additionalElements, tableColumn.AttributeName), 0);
            }
            if (tableColumn.DataType == DataType.Text)
            {
                return Decrypt(GetDcElementContent(additionalElements, tableColumn.AttributeName));
            }
            return GetDcElementContent(additionalElements, tableColumn.AttributeName);
        }

        private void SetValue(ScopedElementCollection additionalElements, TableColumn tableColumn, Entity entity)
        {
            var value = entity.Get(tableColumn.AttributeName)?.ToString();
            if (tableColumn.DataType == DataType.Text)
            {
                value = Encrypt(value);
            }
            AddDcElement(additionalElements, tableColumn.AttributeName, value);
        }

        private string GetMailTemplatesDirectoryPath()
        {
            var plugin = _pluginManager.GetPlugin(PluginId);
            return PathUtils.Combine(plugin.WebRootPath, "assets/form/mail");
        }

        public async Task<string> GetMailTemplateHtmlAsync()
        {
            var directoryPath = GetMailTemplatesDirectoryPath();
            var htmlPath = PathUtils.Combine(directoryPath, "template.html");
            if (_cacheManager.Exists(htmlPath)) return _cacheManager.Get<string>(htmlPath);

            var html = await FileUtils.ReadTextAsync(htmlPath);

            _cacheManager.AddOrUpdate(htmlPath, html);
            return html;
        }

        public async Task<string> GetMailListHtmlAsync()
        {
            var directoryPath = GetMailTemplatesDirectoryPath();
            var htmlPath = PathUtils.Combine(directoryPath, "list.html");
            if (_cacheManager.Exists(htmlPath)) return _cacheManager.Get<string>(htmlPath);

            var html = await FileUtils.ReadTextAsync(htmlPath);

            _cacheManager.AddOrUpdate(htmlPath, html);
            return html;
        }

        public void SendNotify(FormInfo formInfo, List<TableStyle> styles, DataInfo dataInfo)
        {
            //TODO
            //if (formInfo.IsAdministratorSmsNotify &&
            //    !string.IsNullOrEmpty(formInfo.AdministratorSmsNotifyTplId) &&
            //    !string.IsNullOrEmpty(formInfo.AdministratorSmsNotifyMobile))
            //{
            //    var smsPlugin = Context.PluginApi.GetPlugin<SMS.Plugin>();
            //    if (smsPlugin != null && smsPlugin.IsReady)
            //    {
            //        var parameters = new Dictionary<string, string>();
            //        if (!string.IsNullOrEmpty(formInfo.AdministratorSmsNotifyKeys))
            //        {
            //            var keys = formInfo.AdministratorSmsNotifyKeys.Split(',');
            //            foreach (var key in keys)
            //            {
            //                if (FormUtils.EqualsIgnoreCase(key, nameof(DataInfo.Id)))
            //                {
            //                    parameters.Add(key, dataInfo.Id.ToString());
            //                }
            //                else if (FormUtils.EqualsIgnoreCase(key, nameof(DataInfo.AddDate)))
            //                {
            //                    if (dataInfo.AddDate.HasValue)
            //                    {
            //                        parameters.Add(key, dataInfo.AddDate.Value.ToString("yyyy-MM-dd HH:mm"));
            //                    }
            //                }
            //                else
            //                {
            //                    var value = string.Empty;
            //                    var style =
            //                        styleList.FirstOrDefault(x => FormUtils.EqualsIgnoreCase(key, x.Title));
            //                    if (style != null)
            //                    {
            //                        value = LogManager.GetValue(style, dataInfo);
            //                    }

            //                    parameters.Add(key, value);
            //                }
            //            }
            //        }

            //        smsPlugin.Send(formInfo.AdministratorSmsNotifyMobile,
            //            formInfo.AdministratorSmsNotifyTplId, parameters, out _);
            //    }
            //}

            //if (formInfo.IsAdministratorMailNotify &&
            //    !string.IsNullOrEmpty(formInfo.AdministratorMailNotifyAddress))
            //{
            //    var mailPlugin = Context.PluginApi.GetPlugin<Mail.Plugin>();
            //    if (mailPlugin != null && mailPlugin.IsReady)
            //    {
            //        var templateHtml = MailTemplateManager.GetTemplateHtml();
            //        var listHtml = MailTemplateManager.GetListHtml();

            //        var keyValueList = new List<KeyValuePair<string, string>>
            //        {
            //            new KeyValuePair<string, string>("编号", dataInfo.Guid)
            //        };
            //        if (dataInfo.AddDate.HasValue)
            //        {
            //            keyValueList.Add(new KeyValuePair<string, string>("提交时间", dataInfo.AddDate.Value.ToString("yyyy-MM-dd HH:mm")));
            //        }
            //        foreach (var style in styleList)
            //        {
            //            keyValueList.Add(new KeyValuePair<string, string>(style.Title,
            //                LogManager.GetValue(style, dataInfo)));
            //        }

            //        var list = new StringBuilder();
            //        foreach (var kv in keyValueList)
            //        {
            //            list.Append(listHtml.Replace("{{key}}", kv.Key).Replace("{{value}}", kv.Value));
            //        }

            //        var siteInfo = Context.SiteApi.GetSiteInfo(formInfo.SiteId);

            //        mailPlugin.Send(formInfo.AdministratorMailNotifyAddress, string.Empty,
            //            "[SiteServer CMS] 通知邮件",
            //            templateHtml.Replace("{{title}}", $"{formInfo.Title} - {siteInfo.SiteName}").Replace("{{list}}", list.ToString()), out _);
            //    }
            //}

            //if (formInfo.IsUserSmsNotify &&
            //    !string.IsNullOrEmpty(formInfo.UserSmsNotifyTplId) &&
            //    !string.IsNullOrEmpty(formInfo.UserSmsNotifyMobileName))
            //{
            //    var smsPlugin = Context.PluginApi.GetPlugin<SMS.Plugin>();
            //    if (smsPlugin != null && smsPlugin.IsReady)
            //    {
            //        var parameters = new Dictionary<string, string>();
            //        if (!string.IsNullOrEmpty(formInfo.UserSmsNotifyKeys))
            //        {
            //            var keys = formInfo.UserSmsNotifyKeys.Split(',');
            //            foreach (var key in keys)
            //            {
            //                if (FormUtils.EqualsIgnoreCase(key, nameof(DataInfo.Id)))
            //                {
            //                    parameters.Add(key, dataInfo.Id.ToString());
            //                }
            //                else if (FormUtils.EqualsIgnoreCase(key, nameof(DataInfo.AddDate)))
            //                {
            //                    if (dataInfo.AddDate.HasValue)
            //                    {
            //                        parameters.Add(key, dataInfo.AddDate.Value.ToString("yyyy-MM-dd HH:mm"));
            //                    }
            //                }
            //                else
            //                {
            //                    var value = string.Empty;
            //                    var style =
            //                        styleList.FirstOrDefault(x => FormUtils.EqualsIgnoreCase(key, x.Title));
            //                    if (style != null)
            //                    {
            //                        value = LogManager.GetValue(style, dataInfo);
            //                    }

            //                    parameters.Add(key, value);
            //                }
            //            }
            //        }

            //        var mobileFieldInfo = styleList.FirstOrDefault(x => FormUtils.EqualsIgnoreCase(formInfo.UserSmsNotifyMobileName, x.Title));
            //        if (mobileFieldInfo != null)
            //        {
            //            var mobile = LogManager.GetValue(mobileFieldInfo, dataInfo);
            //            if (!string.IsNullOrEmpty(mobile))
            //            {
            //                smsPlugin.Send(mobile, formInfo.UserSmsNotifyTplId, parameters, out _);
            //            }
            //        }
            //    }
            //}
        }

        private string GetTemplatesDirectoryPath()
        {
            var plugin = _pluginManager.GetPlugin(PluginId);
            return PathUtils.Combine(plugin.WebRootPath, "assets/form/templates");
        }

        public List<TemplateInfo> GetTemplateInfoList(string type)
        {
            var templateInfoList = new List<TemplateInfo>();

            var directoryPath = GetTemplatesDirectoryPath();
            var directoryNames = DirectoryUtils.GetDirectoryNames(directoryPath);
            foreach (var directoryName in directoryNames)
            {
                var templateInfo = GetTemplateInfo(directoryPath, directoryName);
                if (templateInfo == null) continue;
                if (StringUtils.EqualsIgnoreCase(type, templateInfo.Type))
                {
                    templateInfoList.Add(templateInfo);
                }
            }

            return templateInfoList;
        }

        public TemplateInfo GetTemplateInfo(string name)
        {
            var directoryPath = GetTemplatesDirectoryPath();
            return GetTemplateInfo(directoryPath, name);
        }

        private TemplateInfo GetTemplateInfo(string templatesDirectoryPath, string name)
        {
            TemplateInfo templateInfo = null;

            var configPath = PathUtils.Combine(templatesDirectoryPath, name, "config.json");
            if (FileUtils.IsFileExists(configPath))
            {
                templateInfo = TranslateUtils.JsonDeserialize<TemplateInfo>(FileUtils.ReadText(configPath));
                templateInfo.Name = name;
            }

            return templateInfo;
        }

        public void Clone(string nameToClone, TemplateInfo templateInfo, string templateHtml = null)
        {
            var plugin = _pluginManager.GetPlugin(PluginId);
            var directoryPath = PathUtils.Combine(plugin.WebRootPath, "assets/form/templates");

            DirectoryUtils.Copy(PathUtils.Combine(directoryPath, nameToClone), PathUtils.Combine(directoryPath, templateInfo.Name), true);

            var configJson = TranslateUtils.JsonSerialize(templateInfo);
            var configPath = PathUtils.Combine(directoryPath, templateInfo.Name, "config.json");
            FileUtils.WriteText(configPath, configJson);

            if (templateHtml != null)
            {
                SetTemplateHtml(templateInfo, templateHtml);
            }
        }

        public void Edit(TemplateInfo templateInfo)
        {
            var plugin = _pluginManager.GetPlugin(PluginId);
            var directoryPath = PathUtils.Combine(plugin.ContentRootPath, "assets/form/templates");

            var configJson = TranslateUtils.JsonSerialize(templateInfo);
            var configPath = PathUtils.Combine(directoryPath, templateInfo.Name, "config.json");
            FileUtils.WriteText(configPath, configJson);
        }

        public string GetTemplateHtml(TemplateInfo templateInfo)
        {
            var directoryPath = GetTemplatesDirectoryPath();
            var htmlPath = PathUtils.Combine(directoryPath, templateInfo.Name, templateInfo.Main);
            return _pathManager.GetContentByFilePath(htmlPath);
        }

        public void SetTemplateHtml(TemplateInfo templateInfo, string html)
        {
            var directoryPath = GetTemplatesDirectoryPath();
            var htmlPath = PathUtils.Combine(directoryPath, templateInfo.Name, templateInfo.Main);

            FileUtils.WriteText(htmlPath, html);
        }

        public void DeleteTemplate(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            var directoryPath = GetTemplatesDirectoryPath();
            var templatePath = PathUtils.Combine(directoryPath, name);
            DirectoryUtils.DeleteDirectoryIfExists(templatePath);
        }

        public List<int> GetRelatedIdentities(int formId)
        {
            return new List<int> { formId };
        }

        public async Task<List<TableStyle>> GetTableStylesAsync(int formId)
        {
            return await _tableStyleRepository.GetTableStylesAsync(FormUtils.TableNameData, GetRelatedIdentities(formId), MetadataAttributes.Value);
        }

        public async Task DeleteTableStyleAsync(int formId, string attributeName)
        {
            await _tableStyleRepository.DeleteAsync(FormUtils.TableNameData, formId, attributeName);
        }

        private static readonly Lazy<List<string>> MetadataAttributes = new Lazy<List<string>>(() => new List<string>
        {
            nameof(DataInfo.FormId),
            nameof(DataInfo.IsReplied),
            nameof(DataInfo.ReplyDate),
            nameof(DataInfo.ReplyContent)
        });

        public int GetPageSize(FormInfo formInfo)
        {
            if (formInfo == null || formInfo.PageSize <= 0) return 30;
            return formInfo.PageSize;
        }
    }
}
