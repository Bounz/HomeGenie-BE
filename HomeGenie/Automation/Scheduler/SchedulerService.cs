﻿/*
    This file is part of HomeGenie Project source code.

    HomeGenie is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    HomeGenie is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with HomeGenie.  If not, see <http://www.gnu.org/licenses/>.
*/

/*
 *     Author: Generoso Martello <gene@homegenie.it>
 *     Project Homepage: http://github.com/Bounz/HomeGenie-BE
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Innovative.SolarCalculator;
using Newtonsoft.Json;
using HomeGenie.Service.Constants;
using System.Text.RegularExpressions;
using NLog;

namespace HomeGenie.Automation.Scheduler
{
    public class SchedulerService
    {
        private const int MAX_EVAL_RECURSION = 4;
        private const string FORMAT_DATETIME = "yyyy-MM-dd HH:mm";
        private const string FORMAT_TIME = "HH:mm";
        private List<SchedulerItem> events = new List<SchedulerItem>();
        private Timer serviceChecker;
        private ProgramManager masterControlProgram;
        private static Logger _log = LogManager.GetCurrentClassLogger();

        public class EvalNode
        {
            public List<DateTime> Occurrences;
            public EvalNode Child;
            public EvalNode Parent;
            public EvalNode Sibling;
            public string Expression;
            public string Operator;
        }

        public SchedulerService(ProgramManager programEngine)
        {
            masterControlProgram = programEngine;
        }

        public void Start()
        {
            Stop();
            serviceChecker = new Timer(CheckScheduledEvents); //, null, 1000, 1000);
            serviceChecker.Change((60-DateTime.Now.Second)*1000, Timeout.Infinite);
        }

        public void Stop()
        {
            if (serviceChecker != null)
            {
                serviceChecker.Dispose();
                for (int i = 0; i < events.Count; i++)
                {
                    var eventItem = events[i];
                    if (eventItem.ScriptEngine != null)
                    {
                        eventItem.ScriptEngine.StopScript();
                    }
                }
            }
        }

        private void CheckScheduledEvents(object state)
        {
            serviceChecker.Change((60-DateTime.Now.Second)*1000, Timeout.Infinite);
            var date = DateTime.Now;
            for (int i = 0; i < events.Count; i++)
            {
                var eventItem = events[i];
                if (eventItem.IsEnabled)
                {
                    // execute items only once instead of repeating for the whole minute
                    string currentOccurrence = date.ToUniversalTime().ToString(FORMAT_DATETIME);
                    if (eventItem.LastOccurrence != currentOccurrence && IsScheduling(date, eventItem.CronExpression))
                    {
                        masterControlProgram.HomeGenie.MigService.RaiseEvent(
                            this,
                            Domains.HomeAutomation_HomeGenie,
                            SourceModule.Scheduler,
                            "Scheduler Event Triggered",
                            Properties.SchedulerTriggeredEvent,
                            eventItem.Name);
                        // update last occurrence value
                        eventItem.LastOccurrence = currentOccurrence;

                        // execute associated task if any
                        if (!String.IsNullOrWhiteSpace(eventItem.Script))
                        {
                            if (eventItem.ScriptEngine == null)
                            {
                                eventItem.ScriptEngine = new SchedulerScriptingEngine();
                                eventItem.ScriptEngine.SetHost(masterControlProgram.HomeGenie, eventItem);
                            }
                            eventItem.ScriptEngine.StartScript();
                        }
                        // TODO: deprecate this! - temporarly left for compatibility with HG <= r522
                        else if (!String.IsNullOrEmpty(eventItem.ProgramId))
                        {
                            var program = masterControlProgram.Programs.Find(p => p.Address.ToString() == eventItem.ProgramId || p.Name == eventItem.ProgramId);
                            if (program != null)
                            {
                                masterControlProgram.HomeGenie.MigService.RaiseEvent(this, Domains.HomeAutomation_HomeGenie, SourceModule.Scheduler, "Scheduler Event '" + eventItem.Name + "'", Properties.SchedulerTriggeredEvent, "'" + eventItem.Name + "' running '" + eventItem.ProgramId + "'");
                                masterControlProgram.Run(program, "");
                            }
                            else
                            {
                                masterControlProgram.HomeGenie.MigService.RaiseEvent(this, Domains.HomeAutomation_HomeGenie, SourceModule.Scheduler, "Scheduler Event '" + eventItem.Name + "'", Properties.SchedulerError, "No such program: '" + eventItem.ProgramId + "'");
                            }
                        }
                    }
                }
            }
        }

        public SchedulerItem Get(string name)
        {
            var eventItem = events.Find(e => e.Name.ToLower() == name.ToLower());
            return eventItem;
        }

        public SchedulerItem AddOrUpdate(string name, string cronExpression, string data = null, string description = null, string script = null)
        {
            if (String.IsNullOrEmpty(name)) return null;
            //
            var eventItem = Get(name);
            bool justAdded = false;
            if (eventItem == null)
            {
                eventItem = new SchedulerItem();
                eventItem.Name = name;
                events.Add(eventItem);
                justAdded = true;
            }
            eventItem.CronExpression = cronExpression;
            if (description != null)
                eventItem.Description = description;
            if (data != null)
                eventItem.Data = data;
            if (script != null)
            {
                if (eventItem.ScriptEngine != null && eventItem.Script != script)
                    eventItem.ScriptEngine.StopScript();
                eventItem.Script = script;
            }
            eventItem.LastOccurrence = "";
            // by default newly added events are enabled
            if (justAdded)
            {
                eventItem.IsEnabled = true;
            }
            return eventItem;
        }
        public bool SetData(string name, string jsonData)
        {
            var eventItem = Get(name);
            if (eventItem != null)
            {
                eventItem.Data = jsonData;
                return true;
            }
            return false;
        }

        [Obsolete()]
        public bool SetProgram(string name, string pid)
        {
            var eventItem = Get(name);
            if (eventItem != null)
            {
                eventItem.ProgramId = pid;
                return true;
            }
            return false;
        }

        public bool SetScript(string name, string script)
        {
            var eventItem = Get(name);
            if (eventItem != null)
            {
                eventItem.Script = script;
                return true;
            }
            return false;
        }

        public bool Enable(string name)
        {
            var eventItem = Get(name);
            if (eventItem != null)
            {
                eventItem.IsEnabled = true;
                return true;
            }
            return false;
        }

        public bool Disable(string name)
        {
            var eventItem = Get(name);
            if (eventItem != null)
            {
                eventItem.IsEnabled = false;
                eventItem.LastOccurrence = "";
                if (eventItem.ScriptEngine != null)
                    eventItem.ScriptEngine.StopScript();
                return true;
            }
            return false;
        }

        public bool Remove(string name)
        {
            var eventItem = Get(name);
            if (eventItem == null)
            {
                return false;
            }
            if (eventItem.ScriptEngine != null)
                eventItem.ScriptEngine.Dispose();
            events.Remove(eventItem);
            return true;
        }

        public bool IsScheduling(DateTime date, string cronExpression, int recursionCount = 0)
        {
            if (date.Kind != DateTimeKind.Local)
                date = date.ToLocalTime();
            var hits = GetScheduling(date.Date, date.Date.AddHours(24).AddMinutes(-1), cronExpression);
            var match = (DateTime?)hits.Find(d => d.ToUniversalTime().ToString(FORMAT_DATETIME) == date.ToUniversalTime().ToString(FORMAT_DATETIME));
            return match != null && match != DateTime.MinValue;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="dateStart"></param>
        /// <param name="dateEnd"></param>
        /// <param name="cronExpression">Cron expression</param>
        /// <param name="recursionCount"></param>
        /// <returns></returns>
        public List<DateTime> GetScheduling(DateTime dateStart, DateTime dateEnd, string cronExpression, int recursionCount = 0)
        {
            // align time
            dateStart = dateStart.AddSeconds((double)-dateStart.Second).AddMilliseconds(-dateStart.Millisecond);
            dateEnd = dateEnd.AddSeconds((double)-dateEnd.Second).AddMilliseconds(-dateEnd.Millisecond);

            // '[' and ']' are just aestethic alias for '(' and ')'
            cronExpression = cronExpression.Replace("[", "(").Replace("]", ")");

            var specialChars = new[] {'(', ')', ' ', ';', '&', ':', '|', '>', '%', '!'};
            var charIndex = 0;
            var rootEvalNode = new EvalNode();
            var evalNode = rootEvalNode;
            while (charIndex < cronExpression.Length)
            {
                var token = cronExpression[charIndex];
                if (token == '\t' || token == '\r' || token == '\n')
                    token = ' ';
                if (specialChars.Contains(token))
                {
                    switch (token)
                    {
                        case '(':
                            evalNode.Child = new EvalNode {Parent = evalNode};
                            evalNode = evalNode.Child;
                            break;

                        case ')':
                            if (evalNode.Parent != null)
                            {
                                evalNode = evalNode.Parent;
                            }
                            else
                            {
                                masterControlProgram.HomeGenie.MigService.RaiseEvent(this, Domains.HomeAutomation_HomeGenie,
                                    SourceModule.Scheduler, cronExpression, Properties.SchedulerError,
                                    JsonConvert.SerializeObject("Unbalanced parenthesis in '" + cronExpression + "'"));
                                return new List<DateTime>();
                            }
                            break;

                        case ';': // AND
                        case '&': // AND
                        case ':': // OR
                        case '|': // OR
                        case '>': // TO
                        case '%': // NOT
                        case '!': // NOT
                            // collect operator and switch to next node
                            evalNode.Operator = token.ToString();
                            evalNode.Sibling = new EvalNode {Parent = evalNode.Parent};
                            evalNode = evalNode.Sibling;
                            break;
                    }

                    charIndex++;
                    continue;
                }

                var currentExpression = token.ToString();
                charIndex++;
                while (charIndex < cronExpression.Length) // collecting plain cron expression
                {
                    token = cronExpression[charIndex];
                    if (specialChars.Except(new []{' '}).Contains(token))
                    {
                        break;
                    }

                    currentExpression += token;
                    charIndex++;
                }

                currentExpression = currentExpression.Trim(' ', '\t');
                if (string.IsNullOrEmpty(currentExpression))
                    continue;

                evalNode.Expression = currentExpression;

                if (currentExpression.StartsWith("#"))
                {
                    // TODO: ...?
                }
                else if (currentExpression.StartsWith("@"))
                {
                    // TODO example
                    // @SolarTimes.Sunset + 30
                    var start = dateStart;
                    var addMinutes = 0;
                    if (currentExpression.IndexOf('+') > 0)
                    {
                        var addMin = currentExpression.Substring(currentExpression.LastIndexOf('+'));
                        addMin = Regex.Replace(addMin, @"\s+", "");
                        addMinutes = int.Parse(addMin);
                        currentExpression = currentExpression.Substring(0, currentExpression.LastIndexOf('+'));
                    }
                    else if (currentExpression.IndexOf('-') > 0)
                    {
                        var addMin = currentExpression.Substring(currentExpression.LastIndexOf('-'));
                        addMin = Regex.Replace(addMin, @"\s+", "");
                        addMinutes = int.Parse(addMin);
                        currentExpression = currentExpression.Substring(0, currentExpression.LastIndexOf('-'));
                    }
                    var eventName = currentExpression.Substring(1);
                    eventName = Regex.Replace(eventName, @"\s+", "");
                    switch (eventName)
                    {

                        #region Built-in events

                        case "SolarTimes.Sunrise":
                            HandleSunrise(evalNode, start, dateEnd, addMinutes);
                            break;

                        case "SolarTimes.Sunset":
                            HandleSunset(evalNode, start, dateEnd, addMinutes);
                            break;

                        case "SolarTimes.SolarNoon":
                            HandleSolarNoon(evalNode, start, dateEnd, addMinutes);
                            break;

                        #endregion Built-in events

                        default:
                        {
                            // Check expresion from scheduled item with a given name
                            var eventItem = Get(eventName);
                            if (eventItem == null)
                            {
                                masterControlProgram.HomeGenie.MigService.RaiseEvent(this,
                                    Domains.HomeAutomation_HomeGenie, SourceModule.Scheduler, cronExpression, Properties.SchedulerError,
                                    JsonConvert.SerializeObject("Unknown event name '" + currentExpression + "'"));
                            }
                            else if (recursionCount >= MAX_EVAL_RECURSION)
                            {
                                recursionCount = 0;
                                masterControlProgram.HomeGenie.MigService.RaiseEvent(this,
                                    Domains.HomeAutomation_HomeGenie, SourceModule.Scheduler, cronExpression, Properties.SchedulerError,
                                    JsonConvert.SerializeObject("Too much recursion in expression '" + currentExpression + "'"));
                                eventItem.IsEnabled = false;
                            }
                            else
                            {
                                recursionCount++;
                                try
                                {
                                    if (eventItem.IsEnabled)
                                    {
                                        evalNode.Occurrences = GetScheduling(dateStart.AddMinutes(-addMinutes),
                                            dateEnd.AddMinutes(-addMinutes), eventItem.CronExpression, recursionCount);
                                        if (addMinutes != 0)
                                        {
                                            for (var o = 0; o < evalNode.Occurrences.Count; o++)
                                            {
                                                evalNode.Occurrences[o] = evalNode.Occurrences[o].AddMinutes(addMinutes);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _log.Error(ex);
                                }

                                recursionCount--;
                                if (recursionCount < 0)
                                    recursionCount = 0;
                            }
                            break;
                        }
                    }
                }
                else
                {
                    evalNode.Occurrences = GetNextOccurrences(dateStart, dateEnd, currentExpression);
                }

            }

            return EvalNodes(rootEvalNode);
        }

        public List<DateTime> EvalNodes(EvalNode currentNode)
        {
            if (currentNode.Occurrences == null)
                currentNode.Occurrences = new List<DateTime>();
            var occurs = currentNode.Occurrences;
            if (currentNode.Child != null)
            {
                occurs = EvalNodes(currentNode.Child);
            }                
            if (currentNode.Sibling != null && currentNode.Operator != null)
            {
                if (currentNode.Operator == ":" || currentNode.Operator == "|")
                {
                    occurs.AddRange(EvalNodes(currentNode.Sibling));
                }
                else if (currentNode.Operator == "%" || currentNode.Operator == "!")
                {
                    var matchList = EvalNodes(currentNode.Sibling);
                    occurs = occurs.Except(matchList).ToList();
                }
                else if (currentNode.Operator == ";" || currentNode.Operator == "&")
                {
                    var matchList = EvalNodes(currentNode.Sibling);
                    //occurs.RemoveAll(dt => !matchList.Contains(dt));
                    occurs = occurs.Intersect(matchList).ToList();
                }
                else if (currentNode.Operator == ">")
                {
                    var matchList = EvalNodes(currentNode.Sibling);
                    if (matchList.Count > 0 && occurs.Count > 0)
                    {
                        var start = occurs.Last();
                        var end = matchList.First();
                        var inc = start.AddMinutes(1).AddSeconds(-start.Second).AddMilliseconds(-start.Millisecond);
                        while (end.ToUniversalTime().ToString(FORMAT_DATETIME) != inc.ToUniversalTime().ToString(FORMAT_DATETIME)) //(Math.Floor((end - inc).TotalMinutes) != 0)
                        {
                            occurs.Add(inc);
                            if (inc.Hour == 23 && inc.Minute == 59)
                            {
                                inc = inc.AddHours(-23);
                                inc = inc.AddMinutes(-59);
                            }
                            else
                            {
                                inc = inc.AddMinutes(1);
                            }
                        }
                        occurs.AddRange(matchList);
                    }
                }
            }
            return occurs;
        }

        public List<SchedulerItem> Items
        {
            get { return events; }
        }

        public dynamic Location
        {
            get
            {
                if (String.IsNullOrWhiteSpace(masterControlProgram.HomeGenie.SystemConfiguration.HomeGenie.Location))
                    masterControlProgram.HomeGenie.SystemConfiguration.HomeGenie.Location = "{ name: 'Rome, RM, Italia', latitude: 41.90278349999999, longitude: 12.496365500000024 }";
                return (dynamic)JsonConvert.DeserializeObject(masterControlProgram.HomeGenie.SystemConfiguration.HomeGenie.Location);
            }
        }

        public void OnModuleUpdate(object eventData)
        {
            foreach (var item in events)
            {
                if (item.ScriptEngine != null)
                {
                    item.ScriptEngine.RouteModuleEvent(eventData);
                }
            }
        }

        private bool EvaluateCronEntry(DateTime date, string cronExpression)
        {
            if (date.Kind != DateTimeKind.Local)
                date = date.ToLocalTime();
            var cronSchedule = NCrontab.CrontabSchedule.TryParse(cronExpression);
            if (!cronSchedule.IsError)
            {
                var occurrence = cronSchedule.Value.GetNextOccurrence(date.AddMinutes(-1));
                string d1 = date.ToUniversalTime().ToString(FORMAT_DATETIME);
                string d2 = occurrence.ToUniversalTime().ToString(FORMAT_DATETIME);
                if (d1 == d2)
                {
                    return true;
                }
            }
            else
            {
                masterControlProgram.HomeGenie.MigService.RaiseEvent(
                    this,
                    Domains.HomeAutomation_HomeGenie,
                    SourceModule.Scheduler,
                    cronExpression,
                    Properties.SchedulerError,
                    JsonConvert.SerializeObject("Syntax error in expression '"+cronExpression+"'"));
            }
            return false;
        }

        private List<DateTime> GetNextOccurrences(DateTime dateStart, DateTime dateEnd, string cronExpression)
        {
            if (dateStart.Kind != DateTimeKind.Local)
                dateStart = dateStart.ToLocalTime();
            if (dateEnd.Kind != DateTimeKind.Local)
                dateEnd = dateEnd.ToLocalTime();
            var cronSchedule = NCrontab.CrontabSchedule.TryParse(cronExpression);
            return !cronSchedule.IsError
                ? cronSchedule.Value.GetNextOccurrences(dateStart.AddMinutes(-1), dateEnd).ToList()
                : null;
        }

        private bool IsBetween(DateTime date, DateTime dateStart, DateTime dateEnd)
        {
            // TODO why is it comparing like a strings???
            var dsr = string.Compare(date.ToUniversalTime().ToString(FORMAT_DATETIME), dateStart.ToUniversalTime().ToString(FORMAT_DATETIME)) >= 0;
            var der = string.Compare(date.ToUniversalTime().ToString(FORMAT_DATETIME), dateEnd.ToUniversalTime().ToString(FORMAT_DATETIME)) <= 0;
            return (dsr && der);
        }

        private void HandleSunrise(EvalNode evalNode, DateTime start, DateTime dateEnd, double addMinutes)
        {
            if (evalNode.Occurrences == null)
                evalNode.Occurrences = new List<DateTime>();
            while (start.Ticks < dateEnd.Ticks)
            {
                var solarTimes = new SolarTimes(start.ToLocalTime(), Location["latitude"].Value, Location["longitude"].Value);
                var sunrise = solarTimes.Sunrise;
                sunrise = sunrise.AddMinutes(addMinutes);
                if (IsBetween(sunrise, start, dateEnd))
                {
                    sunrise = sunrise.AddSeconds(-sunrise.Second).AddMilliseconds(-sunrise.Millisecond);
                    evalNode.Occurrences.Add(sunrise);
                }
                start = start.AddHours(24);
            }
        }

        private void HandleSunset(EvalNode evalNode, DateTime start, DateTime dateEnd, double addMinutes)
        {
            if (evalNode.Occurrences == null)
                evalNode.Occurrences = new List<DateTime>();
            while (start.Ticks < dateEnd.Ticks)
            {
                var solarTimes = new SolarTimes(start.ToLocalTime(), Location["latitude"].Value,
                    Location["longitude"].Value);
                var sunset = solarTimes.Sunset;
                sunset = sunset.AddMinutes(addMinutes);
                if (IsBetween(sunset, start, dateEnd))
                {
                    sunset = sunset.AddSeconds(-sunset.Second).AddMilliseconds(-sunset.Millisecond);
                    evalNode.Occurrences.Add(sunset);
                }
                start = start.AddHours(24);
            }
        }

        private void HandleSolarNoon(EvalNode evalNode, DateTime start, DateTime dateEnd, double addMinutes)
        {
            if (evalNode.Occurrences == null)
                evalNode.Occurrences = new List<DateTime>();
            while (start.Ticks < dateEnd.Ticks)
            {
                var solarTimes = new SolarTimes(start.ToLocalTime(), Location["latitude"].Value,
                    Location["longitude"].Value);
                var solarNoon = solarTimes.SolarNoon;
                solarNoon = solarNoon.AddMinutes(addMinutes);
                if (IsBetween(solarNoon, start, dateEnd))
                {
                    solarNoon = solarNoon.AddSeconds(-solarNoon.Second)
                        .AddMilliseconds(-solarNoon.Millisecond);
                    evalNode.Occurrences.Add(solarNoon);
                }
                start = start.AddHours(24);
            }
        }
    }
}
