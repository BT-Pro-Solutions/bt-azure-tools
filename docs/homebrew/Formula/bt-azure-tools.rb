# typed: strict
# frozen_string_literal: true

# Homebrew formula for BTAzureTools
# Copy this file to your homebrew-tap repository under Formula/bt-azure-tools.rb
class BtAzureTools < Formula
  desc "Azure SQL administration tools for managing Entra ID permissions and more"
  homepage "https://github.com/bt-pro-solutions/bt-azure-tools"
  version "1.0.0"
  license "MIT"

  on_macos do
    on_arm do
      url "https://github.com/bt-pro-solutions/bt-azure-tools/releases/download/v#{version}/bt-azure-tools-osx-arm64.tar.gz"
      sha256 "REPLACE_WITH_ARM64_SHA256"
    end

    on_intel do
      url "https://github.com/bt-pro-solutions/bt-azure-tools/releases/download/v#{version}/bt-azure-tools-osx-x64.tar.gz"
      sha256 "REPLACE_WITH_X64_SHA256"
    end
  end

  def install
    if Hardware::CPU.arm?
      bin.install "bt-azure-tools-osx-arm64" => "bt-azure-tools"
    else
      bin.install "bt-azure-tools-osx-x64" => "bt-azure-tools"
    end
  end

  def caveats
    <<~EOS
      BTAzureTools requires Azure CLI authentication.
      
      Before using, ensure you're logged into Azure:
        az login
      
      For usage information, run:
        bt-azure-tools --help
    EOS
  end

  test do
    assert_match "BTAzureTools", shell_output("#{bin}/bt-azure-tools --version", 0)
  end
end
