﻿using FabricOwl.IConfigs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace FabricOwl
{
    public class RCAEngine
    {
        //Use this class to create the RCAEngine
        public List<ConcurrentEvents> GetSimultaneousEventsForEvent
            (IEnumerable<ConcurrentEventsConfig> configs, dynamic inputEvents, dynamic events, List<ConcurrentEvents> existingEvents = null)
        {
            /*
                Grab the events that occur concurrently with an inputted current event.
            */
            List<ConcurrentEvents> simulEvents = new List<ConcurrentEvents>();

            if (existingEvents != null)
            {

                simulEvents.AddRange(existingEvents);
            }

            // iterate through all the input events
            foreach (dynamic inputEvent in inputEvents)
            {
                if(FindEvent(simulEvents, inputEvent) != null)
                {
                    continue;
                }

                var action = "";
                var reasonForEvent = "";
                ConcurrentEvents reason = null;
                var moreSpecificReason = "";

                foreach (ConcurrentEventsConfig config in configs)
                {
                    string parsed = "";
                    if (config.EventType == inputEvent.Kind.ToString())
                    {
                        // iterate through all events to find relevant ones
                        if(GetPropertyValues(inputEvent, config.Result) != null)
                        {
                            parsed = GetPropertyValues(inputEvent, config.Result);
                            if(config.ResultTransform != null)
                            {
                                parsed = Transformations.getTransformations(config.ResultTransform, parsed);
                            }
                            action = parsed;
                        }

                        reasonForEvent = action;
                        foreach(RelevantEventsConfig relevantEventType in config.RelevantEventsType)
                        {
                            if(relevantEventType.EventType == "self")
                            { // self referential events logic starts here
                                bool propMaps = true;
                                var mappings = relevantEventType.PropertyMappings;
                                foreach(var mapping in mappings)
                                {
                                    object sourceVal = GetPropertyValues(inputEvent, (string)mapping.SourceProperty);
                                    object targetVal = mapping.TargetProperty;

                                    if(mapping.SourceTransform != null)
                                    {
                                        sourceVal = Transformations.getTransformations(mapping.SourceTransform, (string)sourceVal);
                                    }

                                    if(sourceVal == null || targetVal == null || sourceVal != targetVal)
                                    {
                                        propMaps = false;
                                    }
                                }

                                if(propMaps)
                                {
                                    if(relevantEventType.SelfTransform != null)
                                    {
                                        parsed = Transformations.getTransformations(relevantEventType.SelfTransform, parsed);
                                    }

                                    if(reason == null)
                                    {
                                        reason.Name = "self";
                                        reason.RelatedEvent = null;
                                    }
                                    action = parsed;
                                    if(!string.IsNullOrEmpty(relevantEventType.Result))
                                    {
                                        moreSpecificReason = relevantEventType.Result;
                                    }

                                    reasonForEvent = action;
                                }
                            } //self referential events logic ends here
                            foreach(dynamic iterEvent in events) //iterate through other events to find relationships
                            {
                                if (relevantEventType.EventType == iterEvent.Kind.ToString())
                                {
                                    // see if each property mapping holds true
                                    bool valid = true;
                                    var mappings = relevantEventType.PropertyMappings;
                                    foreach (var mapping in mappings)
                                    {
                                        dynamic sourceVal = GetPropertyValues(inputEvent, (string)mapping.SourceProperty);
                                        if (mapping.SourceTransform != null)
                                        {
                                            sourceVal = Transformations.getTransformations(mapping.SourceTransform, (string)sourceVal);
                                        }

                                        dynamic targetVal = GetPropertyValues(iterEvent, (string)mapping.TargetProperty);
                                        if (mapping.TargetTransform != null)
                                        {
                                            targetVal = Transformations.getTransformations(mapping.TargetTransform, (string)targetVal);
                                        }

                                        if (sourceVal == null || targetVal == null || sourceVal != targetVal)
                                        {
                                            valid = false;
                                        }
                                    } //done mapping values and transformations

                                    if (valid)
                                    {
                                        var existingEvent = FindEvent(simulEvents, iterEvent);
                                        if (existingEvent != null)
                                        {
                                            reason = existingEvent;
                                        }
                                        else
                                        {
                                            //generate events to build chain
                                            var reasons = GetSimultaneousEventsForEvent(configs, new List<dynamic> {iterEvent }, events, simulEvents);
                                            foreach (ConcurrentEvents r in reasons)
                                            {
                                                if (FindEvent(simulEvents, r) == null)
                                                {
                                                    simulEvents.Add(r);
                                                }
                                            }
                                            reason = FindEvent(reasons, iterEvent);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                //Create the tempEvent with all the input values
                //Look into EventProperties , EventProperties = inputEvent.EventProperties
                ConcurrentEvents tempEvent = new ConcurrentEvents() { Kind = inputEvent.Kind, Name = inputEvent.Name, RelatedEvent = reason, 
                    ReasonForEvent = (string.IsNullOrEmpty(moreSpecificReason) ? reasonForEvent : moreSpecificReason), EventInstanceId = inputEvent.EventInstanceId, 
                    TimeStamp = inputEvent.TimeStamp, InputEvent = inputEvent};
                simulEvents.Add(tempEvent);
            }
            return simulEvents;
        }

        private ConcurrentEvents FindEvent(List<ConcurrentEvents> events, dynamic tempEvent) {
            return events.FirstOrDefault(e => e.EventInstanceId == tempEvent.EventInstanceId.ToString());
        }

        //Rewrite the Utils.result method
        public static dynamic GetPropertyValues(dynamic item, string propertyPath)
        {
            dynamic prop = item;
            if(!string.IsNullOrEmpty(propertyPath))
            {
                foreach (var path in propertyPath.Split('.'))
                {
                    prop = prop[path];
                }
            }
            return prop != item ? (string)prop : null;

        }
    }
}
