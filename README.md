# The "drain private Azure DevOps agents running on a Azure VM" application

Gracefully drains an Azure VM that is running private Azure DevOps agent of any active jobs. This allows the VM to be restarted saftely without disrupting any running jobs or builds on that VM.

The application is intended to run on the same VM as the Azure DevOps agents. The application subscribes to the `scheduldedEvents` endpoint of [Azure IMDS](https://docs.microsoft.com/en-us/azure/virtual-machines/windows/instance-metadata-service) and polls for the notifications of events impacting the VM i.e. reboot, redploy, terminate etc.

When a scheduled event is detected, the application disables any Azure DevOps agents running on the VM by using the Azure DevOps API. The application does not interact with any agent services locally. With the agents disabled, Azure DevOps will not assign the agents any new jobs but the disabled agents will continue running any in-progress jobs. The application uses the Azure DevOps API to discover if the disabled agents have any in-progress jobs, if there are in-progress jobs, then the application polls until the jobs are finished. 

Once the application has verifed that no jobs are running on the disabled agents, it acknowledges the schdedulded event which allows the scheduled event to take place(restart etc.) 

When restarted, the application will enable **ALL** agents on that VM through the Azure DevOps API.

## Deployment Model

The application is written in .Net Core 3.1

The intended deployment model is one application per VM, the application will only disable agents that running on that VM. 
