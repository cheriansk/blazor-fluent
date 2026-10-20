using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Security;

public class HmacRoleIntegrityTests
{
    private const string SecretKey = "test_cryptographic_secret_key_minimum_32_bytes_long!";

    [Fact]
    public void TenantUserEntity_ValidSignature_PassesVerification()
    {
        var tenantUser = new TenantUserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant_test",
            TenantEntityId = Guid.NewGuid(),
            UserEntityId = Guid.NewGuid(),
            Role = TenantRoleEnum.TenantMember,
            IsActive = true
        };

        tenantUser.RowSignature = tenantUser.ComputeIntegritySignature(SecretKey);

        tenantUser.VerifyIntegritySignature(SecretKey).Should().BeTrue();
    }

    [Fact]
    public void TenantUserEntity_TamperedRole_FailsVerification()
    {
        var tenantUser = new TenantUserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant_test",
            TenantEntityId = Guid.NewGuid(),
            UserEntityId = Guid.NewGuid(),
            Role = TenantRoleEnum.TenantMember,
            IsActive = true
        };

        tenantUser.RowSignature = tenantUser.ComputeIntegritySignature(SecretKey);

        // Attacker attempts direct database SQL update to elevate themselves to TenantAdmin
        tenantUser.Role = TenantRoleEnum.TenantAdmin;

        tenantUser.VerifyIntegritySignature(SecretKey).Should().BeFalse();
    }

    [Fact]
    public void TenantUserEntity_TamperedIsActive_FailsVerification()
    {
        var tenantUser = new TenantUserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant_test",
            TenantEntityId = Guid.NewGuid(),
            UserEntityId = Guid.NewGuid(),
            Role = TenantRoleEnum.TenantMember,
            IsActive = false
        };

        tenantUser.RowSignature = tenantUser.ComputeIntegritySignature(SecretKey);

        // Attacker attempts direct database SQL update to re-activate a revoked account
        tenantUser.IsActive = true;

        tenantUser.VerifyIntegritySignature(SecretKey).Should().BeFalse();
    }

    [Fact]
    public void ProgramUserRoleEntity_ValidSignature_PassesVerification()
    {
        var role = new ProgramUserRoleEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant_test",
            ProgramId = Guid.NewGuid(),
            UserEntityId = Guid.NewGuid(),
            Role = ProgramRoleEnum.ReadOnly
        };

        role.RowSignature = role.ComputeIntegritySignature(SecretKey);

        role.VerifyIntegritySignature(SecretKey).Should().BeTrue();
    }

    [Fact]
    public void ProgramUserRoleEntity_TamperedRole_FailsVerification()
    {
        var role = new ProgramUserRoleEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant_test",
            ProgramId = Guid.NewGuid(),
            UserEntityId = Guid.NewGuid(),
            Role = ProgramRoleEnum.ReadOnly
        };

        role.RowSignature = role.ComputeIntegritySignature(SecretKey);

        // Attacker elevates to Program Admin directly in DB
        role.Role = ProgramRoleEnum.Admin;

        role.VerifyIntegritySignature(SecretKey).Should().BeFalse();
    }

    [Fact]
    public void ProgramUserRoleEntity_TamperedProgramId_FailsVerification()
    {
        var role = new ProgramUserRoleEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant_test",
            ProgramId = Guid.NewGuid(),
            UserEntityId = Guid.NewGuid(),
            Role = ProgramRoleEnum.Admin
        };

        role.RowSignature = role.ComputeIntegritySignature(SecretKey);

        // Attacker changes target program to access another program
        role.ProgramId = Guid.NewGuid();

        role.VerifyIntegritySignature(SecretKey).Should().BeFalse();
    }

    [Fact]
    public void ProjectUserRoleEntity_ValidSignature_PassesVerification()
    {
        var role = new ProjectUserRoleEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant_test",
            ProjectId = Guid.NewGuid(),
            UserEntityId = Guid.NewGuid(),
            Role = ProjectRoleEnum.Contribute
        };

        role.RowSignature = role.ComputeIntegritySignature(SecretKey);

        role.VerifyIntegritySignature(SecretKey).Should().BeTrue();
    }

    [Fact]
    public void ProjectUserRoleEntity_TamperedRole_FailsVerification()
    {
        var role = new ProjectUserRoleEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant_test",
            ProjectId = Guid.NewGuid(),
            UserEntityId = Guid.NewGuid(),
            Role = ProjectRoleEnum.Contribute
        };

        role.RowSignature = role.ComputeIntegritySignature(SecretKey);

        // Attacker elevates to Project Admin
        role.Role = ProjectRoleEnum.Admin;

        role.VerifyIntegritySignature(SecretKey).Should().BeFalse();
    }
}
