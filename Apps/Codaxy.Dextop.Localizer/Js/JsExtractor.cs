﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Codaxy.Dextop.Localizer.Js
{
    public class JsExtractor : EntityExtractor
    {
        static String id = @"[a-zA-Z_][a-zA-Z0-9_]*";
        static String idx = @"[a-zA-Z_][a-zA-Z0-9_.\[\]'" + "\"" + @"]*"; // Literal "
        static String indent = @"([ ]{1,5}|\t)";

        static Regex CreateRegex(String regex)
        {
            return new Regex(regex
                .Replace("{id}", id)
                .Replace("{idx}", idx)
                .Replace("{indent}", indent), RegexOptions.Compiled);
        }

        static Regex lineExtendClass = CreateRegex(@"^(?<name>{id}\.{idx})\s*=\s*Ext\.extend.+"); // e.g. Ext.ux.XYZ = Ext.extend(Ext.ux.XY, {
        static Regex lineNewInstance = CreateRegex(@"^(?<name>{id}\.{idx})\s*=\s*new"); // e.g. eBroker.App = new Ext.app.App({
        static Regex linePlainObject = CreateRegex(@"^(?<name>{id}\.{idx})\s*=\s*{"); // e.g. eBroker.App = {
        static Regex lineSimpleApply = CreateRegex(@"^Ext.apply\(\s*(?<name>({id}\.)?{idx})(\.prototype)?.*"); // e.g. Ext.apply(Dextop.common, {
        static Regex lineForLocalization = CreateRegex(@"^{indent},?(?<name>({id}Text)|title)\s*:\s*(?<value>.+)\s*$"); // e.g. localizationText: 'Text' or title: 'Title'
        static Regex lineExt4Class = CreateRegex(@"^Ext\.define\('(?<name>{id}\.{idx})',\s*{"); // e.g. Ext.define('Ext.ux.XY', { 
        static Regex lineDextopLocalization = CreateRegex(@"^Dextop\.localize\('(?<name>{id}\.{idx})',\s*{"); // e.g. Dextop.localize('Ext.ux.XY', { 

        String GetShortObjectName(String objectName)
        {
            var l = objectName.IndexOf('[');
            if (l == -1)
                return objectName.Substring(objectName.LastIndexOf('.') + 1);
            else
                return objectName.Substring(objectName.Substring(0, l).LastIndexOf('.') + 1);
        }

        ClasslikeEntity ProcessExtendLine(String filePath, String line) {
            var m1 = lineExtendClass.Match(line);
            if (m1.Success)
            {
                var cn = m1.Result("${name}");
                return new ClasslikeEntity {
                    FilePath = filePath,
                    FullEntityName = cn,
                    EntityNameForOverride = cn,
                    ShortEntityName = GetShortObjectName(cn),
                    IsDextopLocalize = true
                };
            }

            var m1_4 = lineExt4Class.Match(line);
            if (m1_4.Success)
            {
                var cn = m1_4.Result("${name}");
                return new ClasslikeEntity
                {
                    FilePath = filePath,
                    FullEntityName = cn,
                    EntityNameForOverride = cn,
                    ShortEntityName = GetShortObjectName(cn),
                    IsDextopLocalize = true
                };
            }

            var m1_d = lineDextopLocalization.Match(line);
            if (m1_d.Success)
            {
                var cn = m1_d.Result("${name}");
                return new ClasslikeEntity
                {
                    FilePath = filePath,
                    FullEntityName = cn,
                    EntityNameForOverride = cn,
                    ShortEntityName = GetShortObjectName(cn),
                    IsDextopLocalize = true
                };
            }

            var m2 = lineSimpleApply.Match(line);
            if (m2.Success)
            {
                var en = m2.Result("${name}"); // Extended name (with ".prototype", if there is any);
                var cn = en.EndsWith(".prototype") ? en.Substring(0, en.LastIndexOf('.')) : en; // Full class name
                return new ClasslikeEntity
                {
                    FilePath = filePath,
                    FullEntityName = cn,
                    EntityNameForOverride = en,
                    ShortEntityName = GetShortObjectName(cn)
                };
            }

            var m3 = lineNewInstance.Match(line);
            if (m3.Success)
            {
                var on = m3.Result("${name}"); // Object name (e.g. eBroker.App)
                return new ClasslikeEntity
                {
                    FilePath = filePath,
                    FullEntityName = on,
                    EntityNameForOverride = on,
                    ShortEntityName = GetShortObjectName(on)
                };
            }

            var m4 = linePlainObject.Match(line);
            if (m4.Success)
            {
                var on = m4.Result("${name}"); // Object name (e.g. eBroker.App)
                return new ClasslikeEntity
                {
                    FilePath = filePath,
                    FullEntityName = on,
                    EntityNameForOverride = on,
                    ShortEntityName = GetShortObjectName(on)
                };
            }

            else
                return null;
        }

        LocalizableEntity ProcessPropertyLine(ClasslikeEntity jsObject, String line)
        {
            var m1 = lineForLocalization.Match(line);
            if (m1.Success)
            {
                var name = m1.Result("${name}");
                var value = m1.Result("${value}").TrimEnd(',', ';', ' ', '\t', '\r', '\n');
                bool quotes = (value.StartsWith("'") && value.EndsWith("'")) || (value.StartsWith("\"") && value.EndsWith("\""));
                return new LocalizableEntity
                {
                    EnclosingEntity = jsObject,
                    EntityName = name,
                    ShallowEntityPath = jsObject.ShortEntityName + "." + name,
                    FullEntityPath = jsObject.EntityNameForOverride + "." + name,
                    IsQuoteEnclosed = quotes,
                    Value = quotes ? value.TrimStart('"', '\'').TrimEnd('"', '\'') : value
                };
            }

            else
                return null;
        }

        public override void ProcessFile(String filePath, Dictionary<String, LocalizableEntity> map)
        {
            
            ClasslikeEntity jsObject = null;
            System.IO.TextReader reader = new System.IO.StreamReader(filePath, Encoding.UTF8);
            //Logger.LogFormat("Processing file {0}", filePath);
            try
            {
                while (reader.Peek() > 0)
                {
                    String line = reader.ReadLine();
                    ClasslikeEntity o = ProcessExtendLine(filePath, line);
                    if (o != null)
                        jsObject = o;

                    else if (jsObject != null) {
                        LocalizableEntity prop = ProcessPropertyLine(jsObject, line);
                        if (prop != null && !map.ContainsKey(prop.FullEntityPath))
                            map.Add(prop.FullEntityPath, prop);
                    }
                }
                Logger.LogFormat("Processing file {0} - Success", filePath);
            }
            catch (Exception ex)
            {
                Logger.LogFormat("Processing file {0} - Error", filePath);
                throw ex;
            }
            finally
            {
                reader.Close();
            }
        }
    }
}
