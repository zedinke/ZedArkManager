namespace ZedASAManager.Services;

public class DockerPermissionService
{
    private readonly SshService _sshService;

    public DockerPermissionService(SshService sshService)
    {
        _sshService = sshService;
    }

    /// <summary>
    /// Ellenőrzi, hogy a felhasználó a docker csoportban van-e, és ha nem, hozzáadja
    /// </summary>
    public async Task<bool> EnsureUserInDockerGroupAsync()
    {
        try
        {
            // Ellenőrizzük, hogy a felhasználó a docker csoportban van-e
            // Használjuk az id -nG parancsot, ami pontosabb
            string checkCommand = "id -nG | grep -q '\\bdocker\\b' && echo 'yes' || echo 'no'";
            string result = await _sshService.ExecuteCommandAsync(checkCommand);
            
            if (result.Trim().Contains("yes"))
            {
                // Már a docker csoportban van, de ellenőrizzük, hogy a jelenlegi session-ben is aktív-e
                // Próbáljuk meg aktiválni a docker csoportot az aktuális session-ben
                try
                {
                    string activateCommand = "newgrp docker <<EOF\necho 'docker group activated'\nEOF";
                    await _sshService.ExecuteCommandAsync(activateCommand);
                }
                catch
                {
                    // Ha newgrp nem működik, próbáljuk meg újra ellenőrizni a jogosultságokat
                }
                return true;
            }

            // Hozzáadjuk a felhasználót a docker csoporthoz
            string username = await GetCurrentUsernameAsync();
            if (string.IsNullOrEmpty(username))
            {
                return false;
            }

            string addCommand = $"sudo usermod -aG docker {username}";
            await _sshService.ExecuteCommandAsync(addCommand);
            
            // Várunk egy kicsit, hogy a változások életbe lépjenek
            await Task.Delay(1000);
            
            // Próbáljuk meg aktiválni a docker csoportot az aktuális session-ben
            // Ez nem mindig működik SSH-n keresztül, de megpróbáljuk
            try
            {
                // A newgrp parancs nem működik jól SSH-n keresztül, de próbáljuk meg
                // Inkább ellenőrizzük, hogy a Docker parancsok most már működnek-e sudo-val
            }
            catch
            {
                // Ignore
            }
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DockerPermissionService: Hiba a docker csoport ellenőrzésekor: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Ellenőrzi, hogy a Docker parancsok működnek-e sudo nélkül
    /// </summary>
    public async Task<bool> CheckDockerPermissionsAsync()
    {
        try
        {
            string testCommand = "docker ps > /dev/null 2>&1 && echo 'ok' || echo 'error'";
            string result = await _sshService.ExecuteCommandAsync(testCommand);
            return result.Trim().Contains("ok");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Wrapper scriptet hoz létre, ami automatikusan kezeli a Docker parancsokat sudo-val
    /// </summary>
    public async Task<string> CreateDockerWrapperScriptAsync(string instanceDirectory)
    {
        try
        {
            string wrapperPath = $"{instanceDirectory}/.docker-wrapper.sh";
            
            string wrapperScript = @"#!/bin/bash
# Docker wrapper script - automatikusan sudo-t használ, ha szükséges
docker_cmd() {
    local cmd=""$1""
    local output
    
    # Először próbáljuk sudo nélkül
    output=$(eval ""$cmd"" 2>&1)
    local exit_code=$?
    
    # Ha permission denied hibát kapunk, próbáljuk sudo-val
    if [ $exit_code -ne 0 ] && [[ ""$output"" =~ ""permission denied"" ]]; then
        output=$(eval ""sudo $cmd"" 2>&1)
        exit_code=$?
    fi
    
    echo ""$output""
    return $exit_code
}

# Exportáljuk a funkciót, hogy a POK-manager.sh is használhassa
export -f docker_cmd

# Futtatjuk az eredeti POK-manager.sh scriptet
exec ""$0"" ""$@""
";

            // Módosítjuk a wrapper scriptet, hogy a POK-manager.sh-t hívja meg
            string actualWrapperScript = $@"#!/bin/bash
# Docker wrapper script - automatikusan sudo-t használ Docker parancsokhoz
# Ez a script a POK-manager.sh körül van, hogy kezelje a Docker permission hibákat

# Helper funkció a Docker parancsokhoz
docker_cmd() {{
    local cmd=""$1""
    local output
    
    # Először próbáljuk sudo nélkül
    output=$(eval ""$cmd"" 2>&1)
    local exit_code=$?
    
    # Ha permission denied hibát kapunk, próbáljuk sudo-val
    if [ $exit_code -ne 0 ] && [[ ""$output"" =~ ""permission denied"" ]]; then
        output=$(eval ""sudo $cmd"" 2>&1)
        exit_code=$?
    fi
    
    echo ""$output""
    return $exit_code
}}

# Exportáljuk a funkciót
export -f docker_cmd

# Futtatjuk az eredeti POK-manager.sh scriptet az összes paraméterrel
cd ""{instanceDirectory}""
exec ./POK-manager.sh ""$@""
";

            // Feltöltjük a wrapper scriptet
            byte[] scriptBytes = System.Text.Encoding.UTF8.GetBytes(actualWrapperScript);
            await _sshService.WriteBinaryFileAsync(wrapperPath, scriptBytes);

            // Executable jogot adunk
            string chmodCommand = $"chmod +x \"{wrapperPath}\"";
            await _sshService.ExecuteCommandAsync(chmodCommand);

            return wrapperPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DockerPermissionService: Hiba a wrapper script létrehozásakor: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task<string> GetCurrentUsernameAsync()
    {
        try
        {
            string command = "whoami";
            string result = await _sshService.ExecuteCommandAsync(command);
            return result.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
