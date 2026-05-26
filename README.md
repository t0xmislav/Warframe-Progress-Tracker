# WIP
Some features may not be complete and are subject to change
admin login: admin admin

# CURRENT FEATURES
## FETCHING ITEMS AND NODES AND MARKING PROGRESS
- Allows fetching of current Warframe items from the Warframestat api
- Allows scraping of mission node info from the Warframe Wiki
- Also able to create your own nodes and items, though it is required to start a fetch from the api in order for the categories to be create first
- All info is stored in a local sqlite database

## DASHBOARD VIEW
- Displays current user progress and calculates the user rank based on cleared items
- The progress display is actively refreshed by threads

## CODEX VIEW
- Allows marking nodes and items as complete
- Filter by name, category and completion status
- Sort by name and date completed
- Allows editing and deleting items/nodes

## ACCOUNT SETTINGS WINDOW
- Able to set download speed limit in the account settings view, enforced by a token bucket algorithm
- Selecting between languages. Languages are set in xaml dictionary files
- Dark and light mode
- Enter your Warframe account name to attempt to fetch it from the Warframestat api or Warframe Market api
- The account name is encrypted with the AES algorithm before being saved in the database. Decrypted on login/fetching the user
- Settings are saved in an ini file

## LOGGING
- Logs are saved in a Json file, admin roles can add an admin note and delete logs

## PDF REPORT
- Able to create a PDF report with a proccess worker
- An xml is created and is read by the process to create a pdf report

## SNAPSHOT
- Able to create an XML snapshot of the current progress
- It is digitally signed with the RSA algorithm
- The signature must be present in the same folder as the XML snapshot to be verified

## DLLs
- MasteryRank library is used to calculate the users rank based on cleared items
- Filters library filters out items while fetching from the api and assigns mastery points for each item

## 
