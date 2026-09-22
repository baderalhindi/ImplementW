# Terraform state for DEV.
#
# The bucket is created by infra/environments/provision-environment.sh (TASK-016) inside dev's
# own project: state holds resource names, addresses and outputs, so PROD state is readable only
# by principals inside the PROD project. A backend block takes no variables, which is why this one
# name is written out rather than read from the manifest; terraform-check.py fails the pull request
# if it stops matching infra/environments/environments.json.

terraform {
  backend "gcs" {
    bucket = "ahda-pmplatform-dev-tfstate"
    prefix = "platform"
  }
}
