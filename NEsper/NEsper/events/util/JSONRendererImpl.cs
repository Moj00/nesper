///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using com.espertech.esper.client;
using com.espertech.esper.client.util;
using com.espertech.esper.compat;
using com.espertech.esper.compat.collections;
using com.espertech.esper.compat.logging;
using com.espertech.esper.compat.magic;

namespace com.espertech.esper.events.util
{
    using Map = IDictionary<String, Object>;

    /// <summary>Render for the JSON format. </summary>
    public class JSONRendererImpl : JSONEventRenderer
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly static String NEWLINE = Environment.NewLine;
        private readonly static String COMMA_DELIMITER_NEWLINE = "," + NEWLINE;

        private readonly RendererMeta _meta;
        private readonly RendererMetaOptions _rendererOptions;

        /// <summary>Ctor. </summary>
        /// <param name="eventType">type of Event(s)</param>
        /// <param name="options">rendering options</param>
        public JSONRendererImpl(EventType eventType, JSONRenderingOptions options)
        {
            EventPropertyRenderer propertyRenderer = null;
            EventPropertyRendererContext propertyRendererContext = null;
            if (options.Renderer != null)
            {
                propertyRenderer = options.Renderer;
                propertyRendererContext = new EventPropertyRendererContext(eventType, true);
            }

            _rendererOptions = new RendererMetaOptions(options.PreventLooping, false, propertyRenderer, propertyRendererContext);
            _meta = new RendererMeta(eventType, new Stack<EventTypePropertyPair>(), _rendererOptions);
        }

        public String Render(String title, EventBean theEvent)
        {
            var buf = new StringBuilder();
            buf.Append('{');
            buf.Append(NEWLINE);

            Ident(buf, 1);
            buf.Append('\"');
            buf.Append(title);
            buf.Append("\": {");
            buf.Append(NEWLINE);

            RecursiveRender(theEvent, buf, 2, _meta, _rendererOptions);

            Ident(buf, 1);
            buf.Append('}');
            buf.Append(NEWLINE);

            buf.Append('}');
            buf.Append(NEWLINE);

            return buf.ToString();
        }

        public String Render(EventBean theEvent)
        {
            StringBuilder buf = new StringBuilder();
            buf.Append('{');
            RecursiveRender(theEvent, buf, 2, _meta, _rendererOptions);
            buf.Append('}');
            return buf.ToString();
        }

        private static void Ident(StringBuilder buf, int level)
        {
            for (int i = 0; i < level; i++)
            {
                IndentChar(buf);
            }
        }

        private static void IndentChar(StringBuilder buf)
        {
            buf.Append(' ');
            buf.Append(' ');
        }

        private static void RecursiveRender(EventBean theEvent, StringBuilder buf, int level, RendererMeta meta, RendererMetaOptions rendererOptions)
        {
            String delimiter = "";

            // simple properties
            GetterPair[] simpleProps = meta.SimpleProperties;
            if (rendererOptions.Renderer == null)
            {
                foreach (GetterPair simpleProp in simpleProps.OrderBy(prop => prop.Name))
                {
                    Object value = simpleProp.Getter.Get(theEvent);
                    WriteDelimitedIndentedProp(buf, delimiter, level, simpleProp.Name);
                    simpleProp.Output.Render(value, buf);
                    delimiter = COMMA_DELIMITER_NEWLINE;
                }
            }
            else
            {
                EventPropertyRendererContext context = rendererOptions.RendererContext;
                context.SetStringBuilderAndReset(buf);
                foreach (GetterPair simpleProp in simpleProps.OrderBy(prop => prop.Name))
                {
                    Object value = simpleProp.Getter.Get(theEvent);
                    WriteDelimitedIndentedProp(buf, delimiter, level, simpleProp.Name);
                    context.DefaultRenderer = simpleProp.Output;
                    context.PropertyName = simpleProp.Name;
                    context.PropertyValue = value;
                    rendererOptions.Renderer.Render(context);
                    delimiter = COMMA_DELIMITER_NEWLINE;
                }
            }

            GetterPair[] indexProps = meta.IndexProperties;
            foreach (GetterPair indexProp in indexProps.OrderBy(prop => prop.Name))
            {
                WriteDelimitedIndentedProp(buf, delimiter, level, indexProp.Name);

                var value = indexProp.Getter.Get(theEvent);
                if (value == null)
                {
                    buf.Append("null");
                }
                else if (value is string)
                {
                    if (rendererOptions.Renderer == null)
                    {
                        indexProp.Output.Render(value, buf);
                    }
                    else
                    {
                        EventPropertyRendererContext context = rendererOptions.RendererContext;
                        context.SetStringBuilderAndReset(buf);
                        context.DefaultRenderer = indexProp.Output;
                        context.PropertyName = indexProp.Name;
                        context.PropertyValue = value;
                        rendererOptions.Renderer.Render(context);
                    }
                }
                else
                {
                    var asArray = value as Array;
                    if (asArray == null)
                    {
                        buf.Append("[]");
                    }
                    else
                    {
                        buf.Append('[');

                        var arrayDelimiter = "";

                        if (rendererOptions.Renderer == null)
                        {
                            for (int i = 0; i < asArray.Length; i++)
                            {
                                var arrayItem = asArray.GetValue(i);
                                buf.Append(arrayDelimiter);
                                indexProp.Output.Render(arrayItem, buf);
                                arrayDelimiter = ", ";
                            }
                        }
                        else
                        {
                            EventPropertyRendererContext context = rendererOptions.RendererContext;
                            context.SetStringBuilderAndReset(buf);
                            for (int i = 0; i < asArray.Length; i++)
                            {
                                Object arrayItem = asArray.GetValue(i);
                                buf.Append(arrayDelimiter);
                                context.PropertyName = indexProp.Name;
                                context.PropertyValue = arrayItem;
                                context.IndexedPropertyIndex = i;
                                context.DefaultRenderer = indexProp.Output;
                                rendererOptions.Renderer.Render(context);
                                arrayDelimiter = ", ";
                            }
                        }
                        buf.Append(']');
                    }
                }
                delimiter = COMMA_DELIMITER_NEWLINE;
            }

            GetterPair[] mappedProps = meta.MappedProperties;
            foreach (GetterPair mappedProp in mappedProps.OrderBy(prop => prop.Name))
            {
                var value = mappedProp.Getter.Get(theEvent);

                if ((value != null) && (!(value.GetType().IsGenericStringDictionary())))
                {
                    Log.Warn("Property '" + mappedProp.Name + "' expected to return Map and returned " + value.GetType() + " instead");
                    continue;
                }

                WriteDelimitedIndentedProp(buf, delimiter, level, mappedProp.Name);

                if (value == null)
                {
                    buf.Append("null");
                    buf.Append(NEWLINE);
                }
                else
                {
                    var map = MagicMarker.GetStringDictionary(value);
                    if (map.IsEmpty())
                    {
                        buf.Append("{}");
                        buf.Append(NEWLINE);
                    }
                    else
                    {
                        buf.Append('{');
                        buf.Append(NEWLINE);

                        var localDelimiter = "";
                        foreach (var entry in map)
                        {
                            if (entry.Key == null)
                            {
                                continue;
                            }

                            buf.Append(localDelimiter);
                            Ident(buf, level + 1);
                            buf.Append('\"');
                            buf.Append(entry.Key);
                            buf.Append("\": ");

                            if (entry.Value == null)
                            {
                                buf.Append("null");
                            }
                            else
                            {
                                OutputValueRenderer outRenderer = OutputValueRendererFactory.GetOutputValueRenderer(entry.Value.GetType(), rendererOptions);
                                if (rendererOptions.Renderer == null)
                                {
                                    outRenderer.Render(entry.Value, buf);
                                }
                                else
                                {
                                    EventPropertyRendererContext context = rendererOptions.RendererContext;
                                    context.SetStringBuilderAndReset(buf);
                                    context.PropertyName = mappedProp.Name;
                                    context.PropertyValue = entry.Value;
                                    context.MappedPropertyKey = entry.Key;
                                    context.DefaultRenderer = outRenderer;
                                    rendererOptions.Renderer.Render(context);
                                }
                            }
                            localDelimiter = COMMA_DELIMITER_NEWLINE;
                        }

                        buf.Append(NEWLINE);
                        Ident(buf, level);
                        buf.Append('}');
                    }
                }

                delimiter = COMMA_DELIMITER_NEWLINE;
            }

            NestedGetterPair[] nestedProps = meta.NestedProperties;
            foreach (NestedGetterPair nestedProp in nestedProps.OrderBy(prop => prop.Name))
            {
                var value = nestedProp.Getter.GetFragment(theEvent);

                WriteDelimitedIndentedProp(buf, delimiter, level, nestedProp.Name);

                if (value == null)
                {
                    buf.Append("null");
                }
                else if (!nestedProp.IsArray)
                {
                    if (!(value is EventBean))
                    {
                        Log.Warn("Property '" + nestedProp.Name + "' expected to return EventBean and returned " + value.GetType() + " instead");
                        buf.Append("null");
                        continue;
                    }
                    var nestedEventBean = (EventBean)value;
                    buf.Append('{');
                    buf.Append(NEWLINE);

                    RecursiveRender(nestedEventBean, buf, level + 1, nestedProp.Metadata, rendererOptions);

                    Ident(buf, level);
                    buf.Append('}');
                }
                else
                {
                    if (!(value is EventBean[]))
                    {
                        Log.Warn("Property '" + nestedProp.Name + "' expected to return EventBean[] and returned " + value.GetType() + " instead");
                        buf.Append("null");
                        continue;
                    }


                    StringBuilder arrayDelimiterBuf = new StringBuilder();
                    arrayDelimiterBuf.Append(',');
                    arrayDelimiterBuf.Append(NEWLINE);
                    Ident(arrayDelimiterBuf, level + 1);

                    EventBean[] nestedEventArray = (EventBean[])value;
                    String arrayDelimiter = "";
                    buf.Append('[');

                    for (int i = 0; i < nestedEventArray.Length; i++)
                    {
                        EventBean arrayItem = nestedEventArray[i];
                        buf.Append(arrayDelimiter);
                        arrayDelimiter = arrayDelimiterBuf.ToString();

                        buf.Append('{');
                        buf.Append(NEWLINE);

                        RecursiveRender(arrayItem, buf, level + 2, nestedProp.Metadata, rendererOptions);

                        Ident(buf, level + 1);
                        buf.Append('}');
                    }
                    buf.Append(']');
                }
                delimiter = COMMA_DELIMITER_NEWLINE;
            }

            buf.Append(NEWLINE);
        }

        private static void WriteDelimitedIndentedProp(StringBuilder buf, String delimiter, int level, String name)
        {
            buf.Append(delimiter);
            Ident(buf, level);
            buf.Append('\"');
            buf.Append(name);
            buf.Append("\": ");
        }
    }
}
