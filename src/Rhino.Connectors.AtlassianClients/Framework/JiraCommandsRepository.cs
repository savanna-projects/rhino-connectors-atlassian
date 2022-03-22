/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Extensions;

using Rhino.Connectors.AtlassianClients.Contracts;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;

namespace Rhino.Connectors.AtlassianClients.Framework
{
    public static class JiraCommandsRepository
    {
        // constants
        private const string ApiVersion = "latest";

        #region *** Get    ***
        /// <summary>
        /// Searches for issues using JQL.
        /// </summary>
        /// <param name="jql">The JQL that defines the search.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        /// <remarks>If no JQL expression is provided, all issues are returned.</remarks>
        public static HttpCommand Search(string jql)
        {
            return new HttpCommand
            {
                Data = new { Jql = jql },
                Method = HttpMethod.Post,
                Route = "/rest/api/" + ApiVersion + "/search"
            };
        }

        /// <summary>
        /// Returns the details for an issue.
        /// </summary>
        /// <param name="idOrKey">The ID or key of the issue.</param>
        /// <param name="fields">A list of fields to return for the issue. Use it to retrieve a subset of fields.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand Get(string idOrKey, params string[] fields)
        {
            // setup
            var queryString = fields.Length > 0 ? $"?fields={string.Join(",", fields)}" : string.Empty;
            var format = "/rest/api/" + ApiVersion + "/issue/{0}" + queryString;

            // get
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = string.Format(format, idOrKey)
            };
        }

        /// <summary>
        /// Returns details of projects, issue types within projects, and, when requested,
        /// the create screen fields for each issue type for the user.
        /// </summary>
        /// <param name="project">The project key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand CreateMeta(string project)
        {
            const string Format = "/rest/api/" + ApiVersion + "/issue/createmeta" +
                "?projectKeys={0}" +
                "&expand=projects.issuetypes.fields";

            // get
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = string.Format(Format, project)
            };
        }

        /// <summary>
        /// Returns either all transitions or a transition that can be performed by
        /// the user on an issue, based on the issue's status.
        /// </summary>
        /// <param name="idOrKey">The ID or key of the issue.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetTransitions(string idOrKey)
        {
            // setup
            const string Format = "/rest/api/" + ApiVersion + "/issue/{0}/transitions";

            // get
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = string.Format(Format, idOrKey)
            };
        }

        /// <summary>
        /// Gets an issue authentication token for the current user/issuer under a given project.
        /// </summary>
        /// <param name="project">The key of the project.</param>
        /// <param name="issue">The key of the issue.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetToken(string project, string issue)
        {
            // setup
            var data = Assembly
                .GetExecutingAssembly()
                .ReadEmbeddedResource("get_token.txt")
                .Replace("[project-key]", project)
                .Replace("[issue-key]", issue);

            // get
            return new HttpCommand
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = "/rest/gira/1/"
            };
        }

        /// <summary>
        /// Gets assignable user by issue key and email.
        /// </summary>
        /// <param name="key">The key of the issue.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetAssignableUsers(string key)
        {
            // setup
            const string Format = "/rest/api/" + ApiVersion + "/user/assignable/search?issueKey={0}";

            // get
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = string.Format(Format, key)
            };
        }
        #endregion

        #region *** Put    ***
        /// <summary>
        /// Updates an issue.
        /// </summary>
        /// <param name="idOrKey">The ID or key of the issue.</param>
        /// <param name="data">Details of an issue update request.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand Update(string idOrKey, object data)
        {
            return GetUpdate(idOrKey, data);
        }

        /// <summary>
        /// Updates an issue comment
        /// </summary>
        /// <param name="idOrKey">The ID or key of the issue.</param>
        /// <param name="comment">Comment to create.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand AddComment(string idOrKey, string comment)
        {
            // setup
            var data = new
            {
                Update = new
                {
                    Comment = new[]
                    {
                        new
                        {
                            Add = new
                            {
                                Body = comment
                            }
                        }
                    }
                }
            };
            return GetUpdate(idOrKey, data);
        }

        private static HttpCommand GetUpdate(string idOrKey, object data)
        {
            return new HttpCommand
            {
                Data = data,
                Method = HttpMethod.Put,
                Route = "/rest/api/" + ApiVersion + $"/issue/{idOrKey}"
            };
        }
        #endregion

        #region *** Post   ***
        /// <summary>
        /// Creates an issue.
        /// </summary>
        /// <param name="data">Details of an issue update request.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand Create(object data)
        {
            return new HttpCommand
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = "/rest/api/" + ApiVersion + "/issue"
            };
        }

        /// <summary>
        /// Creates an link between two issues.
        /// </summary>
        /// <param name="linkType">The name of the link type to create (e.g. Blocks).</param>
        /// <param name="inward">The key of the inward issue (i.e. the issue which blocks).</param>
        /// <param name="outward">The key of the outward issue (i.e. the issue which is blocked by).</param>
        /// <param name="comment">Comment to create for this link.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand CreateIssueLink(string linkType, string inward, string outward, string comment)
        {
            // setup
            var data = new
            {
                Type = new
                {
                    Name = linkType
                },
                InwardIssue = new
                {
                    Key = inward
                },
                OutwardIssue = new
                {
                    Key = outward
                },
                Comment = new
                {
                    Body = comment
                }
            };

            // get
            return new HttpCommand
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = "/rest/api/" + ApiVersion + "/issueLink"
            };
        }

        /// <summary>
        /// Performs an issue transition and, if the transition has a screen, updates the fields from the transition screen.
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="transition">The ID of the transition (you can use GetTransitions method to get the transition ID).</param>
        /// <param name="resolution">The resolution to pass with the transition.</param>
        /// <param name="comment">A comment to add when posting transition.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand CreateTransition(string idOrKey, string transition, string resolution, string comment)
        {
            // setup
            var data = new Dictionary<string, object>
            {
                ["transition"] = new { Id = transition }
            };

            // setup: resolution
            if (!string.IsNullOrEmpty(resolution))
            {
                data["fields"] = new
                {
                    Resolution = new { Name = resolution }
                };
            }

            // setup: comment
            if (!string.IsNullOrEmpty(comment))
            {
                data["update"] = new
                {
                    Comment = new[]
                    {
                        new { Add = new { Body = comment } }
                    }
                };
            }

            // get
            return new HttpCommand
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = $"/rest/api/{ApiVersion}/issue/{idOrKey}/transitions"
            };
        }

        /// <summary>
        /// Assign an issue to a user by user email.
        /// </summary>
        /// <param name="idOrKey">The ID or key of the issue.</param>
        /// <param name="account">The user account ID.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand Assign(string idOrKey, string account)
        {
            // setup
            const string Format = "/rest/api/" + ApiVersion + "/issue/{0}/assignee";

            // get
            return new HttpCommand
            {
                Data = new { AccountId = account },
                Method = HttpMethod.Put,
                Route = string.Format(Format, idOrKey)
            };
        }

        public static HttpCommand CreateWorklog(string id, double timeSpentSeconds, DateTime started, string comment)
        {
            // setup
            var epoch = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            var format = "/rest/api/" + ApiVersion + "/issue/{0}/worklog?_r=" + epoch;
            var requestBody = new
            {
                TimeSpentSeconds = timeSpentSeconds,
                Comment = new
                {
                    Version = 1,
                    Type = "doc",
                    Content = new[]
                    {
                        new
                        {
                            Type = "paragraph",
                            Content = new[]
                            {
                                new
                                {
                                    Type = "text",
                                    Text = comment
                                }
                            }
                        }
                    }
                }
            };

            // get
            return new HttpCommand
            {
                Data = requestBody,
                Method = HttpMethod.Post,
                Route = string.Format(format, id)
            };
        }
        #endregion

        #region *** Delete ***
        /// <summary>
        /// Returns the details for an issue.
        /// </summary>
        /// <param name="id">The ID of the attachment.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand DeleteAttachment(string id)
        {
            // setup
            const string Format = "/rest/api/" + ApiVersion + "/attachment/{0}";

            // get
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Delete,
                Route = string.Format(Format, id)
            };
        }
        #endregion
    }
}