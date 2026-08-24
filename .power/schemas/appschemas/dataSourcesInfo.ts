/*!
 * Copyright (C) Microsoft Corporation. All rights reserved.
 * This file is auto-generated. Do not modify it manually.
 * Changes to this file may be overwritten.
 */

export const dataSourcesInfo = {
  "fmi_businesswrittengroups": {
    "tableId": "",
    "version": "",
    "primaryKey": "fmi_businesswrittengroupid",
    "dataSourceType": "Dataverse",
    "apis": {}
  },
  "fmi_businesswrittenyears": {
    "tableId": "",
    "version": "",
    "primaryKey": "fmi_businesswrittenyearid",
    "dataSourceType": "Dataverse",
    "apis": {}
  },
  "accounts": {
    "tableId": "",
    "version": "",
    "primaryKey": "accountid",
    "dataSourceType": "Dataverse",
    "apis": {}
  },
  "fmi_contents": {
    "tableId": "",
    "version": "",
    "primaryKey": "fmi_contentid",
    "dataSourceType": "Dataverse",
    "apis": {}
  },
  "fmi_dealapprovalitems": {
    "tableId": "",
    "version": "",
    "primaryKey": "fmi_dealapprovalitemid",
    "dataSourceType": "Dataverse",
    "apis": {}
  },
  "fmi_dealapprovals": {
    "tableId": "",
    "version": "",
    "primaryKey": "fmi_dealapprovalid",
    "dataSourceType": "Dataverse",
    "apis": {}
  },
  "fmi_getdealapprovalpreview": {
    "tableId": "",
    "version": "",
    "primaryKey": "",
    "dataSourceType": "Dataverse",
    "apis": {
      "fmi_GetDealApprovalPreview": {
        "path": "/api/data/v9.2/fmi_GetDealApprovalPreview",
        "method": "POST",
        "parameters": [
          {
            "name": "OpportunityId",
            "in": "body",
            "required": true,
            "type": "string"
          }
        ],
        "responseInfo": {
          "200": {
            "type": "object"
          }
        }
      }
    }
  },
  "fmi_getpendingdealapprovals": {
    "tableId": "",
    "version": "",
    "primaryKey": "",
    "dataSourceType": "Dataverse",
    "apis": {
      "fmi_GetPendingDealApprovals": {
        "path": "/api/data/v9.2/fmi_GetPendingDealApprovals",
        "method": "POST",
        "parameters": [],
        "responseInfo": {
          "200": {
            "type": "object"
          }
        }
      }
    }
  },
  "fmi_processdealapprovaldecision": {
    "tableId": "",
    "version": "",
    "primaryKey": "",
    "dataSourceType": "Dataverse",
    "apis": {
      "fmi_ProcessDealApprovalDecision": {
        "path": "/api/data/v9.2/fmi_ProcessDealApprovalDecision",
        "method": "POST",
        "parameters": [
          {
            "name": "fmi_DealApprovalId",
            "in": "body",
            "required": true,
            "type": "string"
          },
          {
            "name": "fmi_Decision",
            "in": "body",
            "required": true,
            "type": "number"
          },
          {
            "name": "fmi_DecisionComment",
            "in": "body",
            "required": true,
            "type": "string"
          }
        ],
        "responseInfo": {
          "200": {
            "type": "object"
          }
        }
      }
    }
  },
  "opportunities": {
    "tableId": "",
    "version": "",
    "primaryKey": "opportunityid",
    "dataSourceType": "Dataverse",
    "apis": {}
  },
  "fmi_opportunityitems": {
    "tableId": "",
    "version": "",
    "primaryKey": "fmi_opportunityitemid",
    "dataSourceType": "Dataverse",
    "apis": {}
  },
  "fmi_targetterritories": {
    "tableId": "",
    "version": "",
    "primaryKey": "fmi_targetterritoryid",
    "dataSourceType": "Dataverse",
    "apis": {}
  },
  "systemusers": {
    "tableId": "",
    "version": "",
    "primaryKey": "systemuserid",
    "dataSourceType": "Dataverse",
    "apis": {}
  }
};
