﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MakeItSoLib;
using Microsoft.VisualStudio.VCProjectEngine;
using VSLangProj80;

namespace SolutionParser_VS2008
{
    /// <summary>
    /// Parses a C# project.
    /// </summary><remarks>
    /// We extract information from a VCProject object, and fill in a Project structure.
    /// </remarks>
    public class ProjectParser_CSharp
    {
        #region Public methods and properties

        /// <summary>
        /// Constructor
        /// </summary>
        public ProjectParser_CSharp(VSProject2 vsProject, string solutionRootFolder)
        {
            try
            {
                m_vsProject = vsProject;
                m_solutionRootFolder = solutionRootFolder;
                m_dteProject = Utils.call<EnvDTE.Project>(() => vsProject.Project);

                // We get the project name...
                m_projectInfo.Name = Utils.call<string>(() => (m_dteProject.Name));
                Log.log("- parsing project " + m_projectInfo.Name);

                // and parse the project...
                parseProject();
                Log.log("  - done");
            }
            catch (Exception ex)
            {
                Log.log(String.Format("  - FAILED ({0})", ex.Message));
            }
        }

        /// <summary>
        /// Gets the parsed project.
        /// </summary>
        public ProjectInfo_CSharp Project 
        {
            get { return m_projectInfo; }
        }

        #endregion

        #region Private functions

        /// <summary>
        /// Parses the project to find the data we need to build the makefile.
        /// </summary>
        private void parseProject()
        {
            parseFiles();
            parseConfigurations();
        }

        /// <summary>
        /// We find the collection of configurations (Release, Debug etc)
        /// and parse each one.
        /// </summary>
        private void parseConfigurations()
        {
            EnvDTE.ConfigurationManager configurationManager = Utils.call<EnvDTE.ConfigurationManager>(() => (m_dteProject.ConfigurationManager));
            int numConfigurations = Utils.call<int>(() => (configurationManager.Count));
            for (int i = 1; i <= numConfigurations; ++i)
            {
                EnvDTE.Configuration dteConfiguration = Utils.call<EnvDTE.Configuration>(() => (configurationManager.Item(i, "") as EnvDTE.Configuration));
                parseConfiguration(dteConfiguration);
            }
        }

        /// <summary>
        /// Parses on configuration.
        /// </summary>
        private void parseConfiguration(EnvDTE.Configuration dteConfiguration)
        {
            // We create a new configuration-info object and fill it in...
            ProjectConfigurationInfo_CSharp configurationInfo = new ProjectConfigurationInfo_CSharp();
            configurationInfo.ParentProjectInfo = m_projectInfo;
            configurationInfo.Name = Utils.call<string>(() => dteConfiguration.ConfigurationName);

            // We parse the configuration's properties, and set configuration
            // seetings from them...
            Dictionary<string, object> properties = getConfigurationProperties(dteConfiguration);
            configurationInfo.Optimize = getBoolProperty(properties, "Optimize");
            configurationInfo.OutputFolder = getStringProperty(properties, "OutputPath");
            configurationInfo.ThreatWarningsAsErrors = getBoolProperty(properties, "TreatWarningsAsErrors");
            string definedConstants = getStringProperty(properties, "DefineConstants");
            foreach (string definedConstant in Utils.split(definedConstants, ';'))
            {
                configurationInfo.addDefinedConstant(definedConstant);
            }
            configurationInfo.Debug = getBoolProperty(properties, "DebugSymbols");
            string noWarn = getStringProperty(properties, "NoWarn");

            // We add the configuration-info to the project-info...
            m_projectInfo.addConfigurationInfo(configurationInfo);
        }

        /// <summary>
        /// Gets a bool property from the collection of properties passed in.
        /// Returns false if the property is not in the collection.
        /// </summary>
        private bool getBoolProperty(Dictionary<string, object> properties, string name)
        {
            return (properties.ContainsKey(name) == true) ? (bool)properties[name] : false;
        }

        /// <summary>
        /// Gets a string property from the collection of properties passed in.
        /// Returns "" if the property is not in the collection.
        /// </summary>
        private string getStringProperty(Dictionary<string, object> properties, string name)
        {
            return (properties.ContainsKey(name) == true) ? (string)properties[name] : "";
        }

        /// <summary>
        /// Converts the collection of properties for the configuration passed in,
        /// into a map of string -> object.
        /// </summary>
        private Dictionary<string, object> getConfigurationProperties(EnvDTE.Configuration dteConfiguration)
        {
            Dictionary<string, object> results = new Dictionary<string, object>();

            EnvDTE.Properties dteProperties = Utils.call<EnvDTE.Properties>(() => (dteConfiguration.Properties));
            int numProperties = Utils.call<int>(() => (dteProperties.Count));
            for (int i = 1; i <= numProperties; ++i)
            {
                EnvDTE.Property dteProperty = Utils.call<EnvDTE.Property>(() => (dteProperties.Item(i)));
                string propertyName = Utils.call<string>(() => (dteProperty.Name));
                object propertyValue = Utils.call<object>(() => (dteProperty.Value));
                results[propertyName] = propertyValue;
            }

            return results;
        }

        /// <summary>
        /// Finds the collection of .cs files in the project.
        /// </summary>
        private void parseFiles()
        {
            // We find the collection of files...
            List<string> files = new List<string>();
            EnvDTE.ProjectItems projectItems = Utils.call<EnvDTE.ProjectItems>(() => (m_dteProject.ProjectItems));
            findFiles(projectItems, files, "");

            // We add the files to the project info...
            foreach (string file in files)
            {
                m_projectInfo.addFile(file);
            }
        }


        /// <summary>
        /// Find all .cs files in the project, including sub-folders.
        /// </summary>
        private void findFiles(EnvDTE.ProjectItems projectItems, List<string> files, string path)
        {
            // We look through the items...
            int numProjectItems = Utils.call<int>(() => (projectItems.Count));
            for (int i = 1; i <= numProjectItems; ++i)
            {
                EnvDTE.ProjectItem projectItem = Utils.call<EnvDTE.ProjectItem>(() => (projectItems.Item(i)));
                string itemName = Utils.call<string>(() => projectItem.Name);
                if (itemName.EndsWith(".cs") == true)
                {
                    string filePath = path + itemName;
                    files.Add(filePath);
                }

                // We see if the item itself has sub-items...
                EnvDTE.ProjectItems subItems = Utils.call<EnvDTE.ProjectItems>(() => (projectItem.ProjectItems));
                if (subItems != null)
                {
                    string newPath = path + itemName + "/";
                    findFiles(subItems, files, newPath);
                }

                // We see if this item has a sub-project...
                EnvDTE.Project subProject = Utils.call<EnvDTE.Project>(() => (projectItem.SubProject));
                if (subProject != null)
                {
                    EnvDTE.ProjectItems subProjectItems = Utils.call<EnvDTE.ProjectItems>(() => (subProject.ProjectItems));
                    string newPath = path + itemName + "/";
                    findFiles(subProjectItems, files, newPath);
                }
            }
        }

        #endregion

        #region Private data

        // Holds the parsed project data...
        private ProjectInfo_CSharp m_projectInfo = new ProjectInfo_CSharp();

        // The root folder of the solution that this project is part of...
        private string m_solutionRootFolder = "";

        // The Visual Studio project objects. We need two of these: the
        // EnvDte project which as overall project info, and the VSProject2
        // which has C#-specific info...
        private EnvDTE.Project m_dteProject = null;
        private VSProject2 m_vsProject = null;

        #endregion
    }
}
