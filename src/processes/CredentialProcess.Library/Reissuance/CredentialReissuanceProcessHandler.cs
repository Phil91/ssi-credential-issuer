﻿/********************************************************************************
 * Copyright (c) 2024 Contributors to the Eclipse Foundation
 *
 * See the NOTICE file(s) distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This program and the accompanying materials are made available under the
 * terms of the Apache License, Version 2.0 which is available at
 * https://www.apache.org/licenses/LICENSE-2.0.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 ********************************************************************************/

using Microsoft.Extensions.Logging;
using Org.Eclipse.TractusX.SsiCredentialIssuer.DBAccess;
using Org.Eclipse.TractusX.SsiCredentialIssuer.DBAccess.Repositories;
using Org.Eclipse.TractusX.SsiCredentialIssuer.Entities.Enums;

namespace Org.Eclipse.TractusX.SsiCredentialIssuer.CredentialProcess.Library;

public class CredentialReissuanceProcessHandler : ICredentialReissuanceProcessHandler
{
    private readonly IIssuerRepositories _issuerRepositories;
    private readonly ILogger<CredentialReissuanceProcessHandler> _logger;

    public CredentialReissuanceProcessHandler(IIssuerRepositories issuerRepositories, ILogger<CredentialReissuanceProcessHandler> logger)
    {
        _issuerRepositories = issuerRepositories;
        _logger = logger;
    }

    public Task<(IEnumerable<ProcessStepTypeId>? nextStepTypeIds, ProcessStepStatusId stepStatusId, bool modified, string? processMessage)> RevokeReissuedCredential(Guid credentialId)
    {
        var isReissuedCredential = _issuerRepositories.GetInstance<IReissuanceRepository>().IsReissuedCredential(credentialId);

        if (isReissuedCredential)
        {
            CreateRevokeCredentialProcess(credentialId);
        }

        (IEnumerable<ProcessStepTypeId>? nextStepTypeIds, ProcessStepStatusId stepStatusId, bool modified, string? processMessage) result = (
            Enumerable.Repeat(ProcessStepTypeId.SAVE_CREDENTIAL_DOCUMENT, 1),
            ProcessStepStatusId.DONE,
            false,
            null);

        return Task.FromResult(result);
    }

    private void CreateRevokeCredentialProcess(Guid credentialId)
    {
        var companySsiRepository = _issuerRepositories.GetInstance<ICompanySsiDetailsRepository>();
        var processStepRepository = _issuerRepositories.GetInstance<IProcessStepRepository>();
        var processId = processStepRepository.CreateProcess(ProcessTypeId.DECLINE_CREDENTIAL).Id;
        var credentialToRevokeId = _issuerRepositories.GetInstance<IReissuanceRepository>().GetCompanySsiDetailId(credentialId);

        try
        {
            processStepRepository.CreateProcessStep(ProcessStepTypeId.REVOKE_CREDENTIAL, ProcessStepStatusId.TODO, processId);
            companySsiRepository.AttachAndModifyCompanySsiDetails(credentialToRevokeId, c =>
            {
                c.ProcessId = null;
            },
            c =>
            {
                c.ProcessId = processId;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("The revokation of the reissued credential failed with error: {Errors}", ex.Message);
        }
    }
}
