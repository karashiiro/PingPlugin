# CMakeList.txt : CMake project for OSBindings, include source and define
# project specific logic here.
#
cmake_minimum_required (VERSION 3.8)
project(OSBindings)

set(CMAKE_CXX_STANDARD 17)

# Add sources to this project's library.
set(src ${CMAKE_SOURCE_DIR}/src)
set(src_files
	${src}/OSBindings.h
)
set(win_bindings ${src}/WinBindings.cpp)

# Define target
add_library(OSBindings SHARED ${src_files} ${win_bindings})

# Target link libraries
set(win_lib Ws2_32.lib Iphlpapi.lib)

target_link_libraries(OSBindings PRIVATE ${win_lib})