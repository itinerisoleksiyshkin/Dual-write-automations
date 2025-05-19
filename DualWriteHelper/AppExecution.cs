// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.Json.Nodes;
using DWLibary.Struct;
using DWLibary;
using static System.Net.Mime.MediaTypeNames;
using DWLibary.Engines;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using DWHelper;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using System.Diagnostics;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;

namespace DWHelper
{
    public class AppExecution
    {
       
        ILogger logger;
        IHostApplicationLifetime lifeTime;

        public AppExecution(ILogger _logger, IHostApplicationLifetime _lifetime)
        {
            logger = _logger;
            //exportConfig = false;
            lifeTime = _lifetime;
        }

        public void run()
        {
            try
            {

                GlobalVar.init(logger);

                //string test = MFAGen.getMFAKey();

                //check if the given username is a user or a client id: 
                //ClientId won't work

                


                if (GlobalVar.runMode == DWEnums.RunMode.compare)
                {
                    //do something
                    if(GlobalVar.parsedOptions.targetenvironment == null || GlobalVar.parsedOptions.targetenvironment == String.Empty)
                    {
                        logger.LogError("Target environment is empty, for comparison specify a target with -t");
                        lifeTime.StopApplication();
                        return;
                    }

                    DWComparison dWComparison = new DWComparison(GlobalVar.foEnv, GlobalVar.parsedOptions.targetenvironment, logger);
                    dWComparison.runComparison().Wait();
                    logger.LogInformation("Comparison complete");
                    lifeTime.StopApplication();
                    return;
                }

                if (!GlobalVar.username.Contains("@"))
                {
                    //Client / Secret auth

                    ServicePrincipalAuth servicePrincipalAuth = new ServicePrincipalAuth(logger);
                    if(!servicePrincipalAuth.authenticate().Result)
                        return;

                }
                else
                {

                    //user based authentication

                    logger.LogInformation("Get access token, opening Edge");

                    checkEdgeVersionAndRetrieveToken();

                }

                TokenRefresh tokenRefresh = new TokenRefresh(logger);
                tokenRefresh.run();

                logger.LogInformation("Get Environment");

                DWEnvCalls dWEnvCalls = new DWEnvCalls();
                DWEnvironment dwEnv = dWEnvCalls.getEnvironment().Result;

                if (dwEnv.cid == null || dwEnv.cid.Length == 0)
                {
                    logger.LogInformation("Environment is not linked, exiting");
                    lifeTime.StopApplication();
                    return;
                }


                //now do the Wiki Upload
                //DWADOWikiEngine adoWiki = new DWADOWikiEngine(dwEnv, logger);
                //adoWiki.runWikiUpload().Wait();

                logger.LogInformation($"Runmode: {GlobalVar.runMode}");

                DWMapEngine mapEngine = new DWMapEngine(dwEnv, logger);

                switch(GlobalVar.runMode)
                {
                    case DWEnums.RunMode.export:
                        logger.LogInformation("Exporting config parameter is true");
                        mapEngine.generateMapConfig().Wait();
                        break;

                    case DWEnums.RunMode.wikiUpload:
                        DWADOWikiEngine adoWiki = new DWADOWikiEngine(dwEnv, logger);
                        adoWiki.runWikiUpload(true).Wait();
                        break;

                    case DWEnums.RunMode.resetLink:
                        ResetLinkEngine resetLink = new ResetLinkEngine(logger, dwEnv);
                        resetLink.resetLink(GlobalVar.parsedOptions.forceReset).Wait();

                        break;

                    default:
                        if (!GlobalVar.noSolutions)
                        {
                            DWSolutionEngine dWSolution = new DWSolutionEngine(dwEnv, logger);
                            dWSolution.applySolutions().Wait();
                        }


                        mapEngine.applyMaps().Wait();


                        // now do the Wiki Upload
                        DWADOWikiEngine adoWikiDeploy = new DWADOWikiEngine(dwEnv, logger);
                        adoWikiDeploy.runWikiUpload().Wait();
                        break;
                }


               




            }
            catch (Exception ex)
            {
                GlobalVar.addError(ex.ToString());
            }
            
            lifeTime.StopApplication();
        }



        public void reAuthenticate()
        {

            checkEdgeVersionAndRetrieveToken();

        }

        private void checkEdgeVersionAndRetrieveToken()
        {
            EdgeUniversal uni = new EdgeUniversal(logger);
            uni.getToken();
            
        }
        

    }
}
