@echo off
rem Arguments (quoted by the editor): input file, output file, c/l, parent folder, headers folder, definition
set "fscript=%~1"
set "pfile=%~2"
set param=%3
set "scrpath=%~4"
set "path=%~5"
set def=%6

echo Include path: %path%
echo Script preprocessing path: %fscript%

wcc386.exe "%fscript%" -p%param% -fr=wcc -eq -ef -fo="%pfile%" -i="%path%" -i="%scrpath%" %def%

if not exist "%pfile%" goto FAILED
goto DONE

:FAILED
Exit 1
:DONE
