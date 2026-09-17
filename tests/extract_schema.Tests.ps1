BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot ".." "extract_schema.ps1"
}

Describe "extract_schema.ps1 Parameter Validation" {
    Context "When database file does not exist" {
        It "Throws an error stating the file was not found" {
            $nonExistentPath = Join-Path ([System.IO.Path]::GetTempPath()) "non_existent_$(Get-Random).mdb"

            { & $scriptPath -MdbPath $nonExistentPath -ErrorAction Stop } | Should -Throw "*File not found*"
        }

        It "Does not create output directory when database file is missing" {
            $nonExistentPath = Join-Path ([System.IO.Path]::GetTempPath()) "non_existent_$(Get-Random).mdb"
            $testOutputDir = Join-Path ([System.IO.Path]::GetTempPath()) "test_output_dir_$(Get-Random)"

            try {
                { & $scriptPath -MdbPath $nonExistentPath -OutputDir $testOutputDir -ErrorAction Stop } | Should -Throw
                Test-Path $testOutputDir | Should -Be $false
            }
            finally {
                if (Test-Path $testOutputDir) {
                    Remove-Item -Path $testOutputDir -Recurse -Force
                }
            }
        }
    }
}
