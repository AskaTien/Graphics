from ruamel.yaml.scalarstring import DoubleQuotedScalarString as dss
from ..shared.namer import *
from ..shared.yml_job import YMLJob

class ABV_AllProjectCiJob():
    
    def __init__(self, editor, projects, abv_trigger_editors):
        self.job_id = abv_job_id_all_project_ci(editor["version"])
        self.yml = self.get_job_definition(editor, projects, abv_trigger_editors).get_yml()

    
    def get_job_definition(self, editor, projects, abv_trigger_editors): 
    
        # define dependencies
        dependencies = [{
            'path': f'{packages_filepath()}#{package_job_id_test_all(editor["version"])}',
            'rerun': 'always'}]

        for project in projects:
            dependencies.append({
                'path': f'{project_filepath_all(project["name"])}#{project_job_id_all(project["name"], editor["version"])}',
                'rerun': 'always'})

        # construct job
        job = YMLJob()
        job.set_name(f'_ABV for SRP repository - {editor["version"]}')
        job.add_dependencies(dependencies)
        job.add_var_custom_revision(editor["version"])
        if editor['version'] in abv_trigger_editors:
            job.set_trigger_on_expression('pull_request.target eq "master" AND NOT pull_request.draft AND NOT pull_request.push.changes.all match ["**/*.md", "doc/**/*", "**/Documentation*/**/*"]')
        return job